using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace mil.Core
{
    public static class MidiTrackParser
    {
        public struct MidiNoteData
        {
            public float TimestampMs;
            public float DurationMs;
            public int NoteType;
            public bool IsHoldNote;
        }

        public struct GeneratedTimelineData
        {
            public MidiNoteData[] Notes;
        }

        public static GeneratedTimelineData ParseMidiFile(string relativePath)
        {
            if (!relativePath.EndsWith(".mid", StringComparison.OrdinalIgnoreCase)) relativePath += ".mid";
            relativePath = relativePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            string fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);

            var failData = new GeneratedTimelineData { Notes = new MidiNoteData[0] };
            if (!File.Exists(fullPath))
            {
                Debug.LogError($"[MidiParser] Arquivo não encontrado em: {fullPath}");
                return failData;
            }

            List<MidiNoteData> extractedNotes = new();

            // CORREÇÃO DE INFRAESTRUTURA: Nomeamos explicitamente as variáveis da tupla (startTime e startVelocity)
            // Isso garante que o compilador da Unity encontre os nomes corretos no bloco de leitura abaixo!
            Dictionary<int, (float startTime, int startVelocity)> openNotes = new();

            try
            {
                using var stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var reader = new BinaryReader(stream);

                string headerType = new string(reader.ReadChars(4));
                if (headerType != "MThd") return failData;

                ReadNetworkOrderInt32(reader);
                short format = ReadNetworkOrderInt16(reader);
                short trackCount = ReadNetworkOrderInt16(reader);
                short division = ReadNetworkOrderInt16(reader); // PPQ

                float currentBpm = 120f;

                for (int t = 0; t < trackCount; t++)
                {
                    if (stream.Position >= stream.Length) break;

                    string trackType = new string(reader.ReadChars(4));
                    if (trackType != "MTrk") continue;

                    int trackLength = ReadNetworkOrderInt32(reader);
                    byte[] trackData = reader.ReadBytes(trackLength);

                    ParseTrackBytes(trackData, division, ref currentBpm, openNotes, extractedNotes);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MidiParser] Erro Crítico ao processar binário: {ex.Message}\n{ex.StackTrace}");
                return failData;
            }

            extractedNotes.Sort((a, b) => a.TimestampMs.CompareTo(b.TimestampMs));

            Debug.Log($"[MidiParser] Sucesso! Extraídas {extractedNotes.Count} notas robustas do Guitar Pro 8.");
            return new GeneratedTimelineData { Notes = extractedNotes.ToArray() };
        }

        private static void ParseTrackBytes(byte[] data, short ppq, ref float currentBpm, Dictionary<int, (float startTime, int startVelocity)> openNotes, List<MidiNoteData> extractedNotes)
        {
            int index = 0;
            long absoluteTicks = 0;
            byte runningStatus = 0;
            int lastPitch = -1;

            while (index < data.Length)
            {
                long deltaTimeTicks = ReadVariableLengthValue(data, ref index);
                absoluteTicks += deltaTimeTicks;
                if (index >= data.Length) break;

                byte statusByte = data[index];

                if ((statusByte & 0x80) != 0)
                {
                    runningStatus = statusByte;
                    index++;
                }

                byte commandType = (byte)(runningStatus & 0xF0);

                if (runningStatus == 0xFF)
                {
                    byte metaType = data[index++];
                    long metaLength = ReadVariableLengthValue(data, ref index);

                    if (metaType == 0x51)
                    {
                        int microsecondsPerBeat = (data[index] << 16) | (data[index + 1] << 8) | data[index + 2];
                        currentBpm = 60000000f / microsecondsPerBeat;
                    }

                    index += (int)metaLength;
                }
                else if (runningStatus == 0xF0 || runningStatus == 0xF7)
                {
                    long sysexLength = ReadVariableLengthValue(data, ref index);
                    index += (int)sysexLength;
                }
                else
                {
                    int pitch = data[index++];
                    int velocity = 0;

                    if (commandType != 0xC0 && commandType != 0xD0)
                    {
                        velocity = data[index++];
                    }

                    float currentTimeInMs = (absoluteTicks * 60000f) / (currentBpm * ppq);

                    if (commandType == 0x90 && velocity > 0)
                    {
                        openNotes[pitch] = (currentTimeInMs, velocity);
                    }
                    // Dentro do seu MidiTrackParser.cs -> método ParseTrackBytes -> no bloco de Note Off:
                    else if (commandType == 0x80 || (commandType == 0x90 && velocity == 0))
                    {
                        if (openNotes.TryGetValue(pitch, out var startData))
                        {
                            currentTimeInMs = (absoluteTicks * 60000f) / (currentBpm * ppq);
                            float durationMs = currentTimeInMs - startData.startTime;
                            openNotes.Remove(pitch);

                            // Classificação de tipos por Velocity/Pitch original do GDD
                            int assignedType;
                            if ((pitch >= 124 && pitch <= 126 || startData.startVelocity >= 1 && startData.startVelocity <= 15) || (startData.startVelocity >= 1 && startData.startVelocity <= 45))
                            {
                                assignedType = 4; // DEAD Note do Guitar Pro 8
                            }
                            else
                            {
                                if (lastPitch == -1) assignedType = UnityEngine.Random.Range(0, 4);
                                else
                                {
                                    int pitchDelta = pitch - lastPitch;
                                    // Change 4 : UnityEngine.Random.Range(0, 4); to 5 : UnityEngine.Random.Range(0, 4); If we want implement WRONG NOTES
                                    if (pitchDelta == 0) assignedType = (UnityEngine.Random.Range(0, 100) < 15) ? 4 : UnityEngine.Random.Range(0, 4);
                                    else if (pitchDelta > 0) assignedType = pitchDelta > 2 ? 3 : 2;
                                    else assignedType = pitchDelta < -2 ? 0 : 1;
                                }
                                lastPitch = pitch;
                            }

                            // -----------------------------------------------------------------
                            // CALIBRAÇÃO DO LIMIAR SOLICITADO (1000ms Fixo de Segurança)
                            // -----------------------------------------------------------------
                            // Notas com duração igual ou maior que 1 segundo (1000ms) viram Hold Notes!
                            bool isHold = durationMs >= 1000f;

                            // Regra dos 80% de janela de conforto para o jogador soltar o dedo
                            float finalDurationMs = isHold ? (durationMs * 0.80f) : durationMs;

                            extractedNotes.Add(new MidiNoteData
                            {
                                TimestampMs = startData.startTime,
                                DurationMs = finalDurationMs > 10f ? finalDurationMs : 400f,
                                NoteType = assignedType,
                                IsHoldNote = isHold // Retorna a variável matemática legítima!
                            });
                        }
                    }

                }
            }
        }

        private static int ReadNetworkOrderInt32(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }

        private static short ReadNetworkOrderInt16(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(2);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToInt16(bytes, 0);
        }

        private static long ReadVariableLengthValue(byte[] data, ref int index)
        {
            long value = 0;
            byte b;
            do
            {
                b = data[index++];
                value = (value << 7) | (b & 0x7F);
            } while ((b & 0x80) != 0);
            return value;
        }
    }
}
