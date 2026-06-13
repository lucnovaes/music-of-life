using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace mil.Core
{
    public static class MidiTrackParser
    {
        public struct GeneratedTimelineData
        {
            public float[] TimestampsMs;
            public int[] NoteTypes;
        }

        public static GeneratedTimelineData ParseMidiFile(string relativePath)
        {
            // Garante a inserção automática da extensão
            if (!relativePath.EndsWith(".mid", StringComparison.OrdinalIgnoreCase))
            {
                relativePath += ".mid";
            }

            relativePath = relativePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            string fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);

            // Retorno seguro padrão (Arrays vazias em vez de nulo para evitar NullReferenceException)
            var failData = new GeneratedTimelineData { TimestampsMs = new float[0], NoteTypes = new int[0] };

            if (!File.Exists(fullPath))
            {
                Debug.LogError($"[MidiParser] Arquivo MIDI não encontrado em: {fullPath}");
                return failData;
            }

            List<float> timestamps = new();
            List<int> types = new();

            try
            {
                using var stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var reader = new BinaryReader(stream);

                // 1. Validar cabeçalho do arquivo
                char[] headerId = reader.ReadChars(4);
                if (new string(headerId) != "MThd")
                {
                    Debug.LogError("[MidiParser] Erro: O cabeçalho 'MThd' do arquivo não é um MIDI padrão válido.");
                    return failData;
                }

                int headerLength = ReadNetworkOrderInt32(reader);
                short format = ReadNetworkOrderInt16(reader);
                short trackCount = ReadNetworkOrderInt16(reader);
                short division = ReadNetworkOrderInt16(reader); // PPQ (Ticks por batida)

                if ((division & 0x8000) != 0)
                {
                    Debug.LogError("[MidiParser] O arquivo usa codificação SMPTE que não é suportada por este motor.");
                    return failData;
                }

                float currentBpm = 120f;
                int lastPitch = -1;

                // 2. Varrer todas as trilhas (Tracks) contidas no arquivo de forma profunda
                for (int t = 0; t < trackCount; t++)
                {
                    if (stream.Position >= stream.Length) break;

                    char[] trackId = reader.ReadChars(4);
                    int trackLength = ReadNetworkOrderInt32(reader);

                    if (new string(trackId) != "MTrk")
                    {
                        // Se não for um bloco MTrk legítimo, pula os bytes de metadados desconhecidos
                        stream.Seek(trackLength, SeekOrigin.Current);
                        continue;
                    }

                    byte[] trackData = reader.ReadBytes(trackLength);
                    ParseTrackData(trackData, division, ref currentBpm, ref lastPitch, timestamps, types);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MidiParser] Exceção crítica ao ler os bytes do arquivo binário: {ex.Message}");
                return failData;
            }

            // Entrega os dados formatados com segurança sem risco de nulos
            return new GeneratedTimelineData
            {
                TimestampsMs = timestamps.ToArray(),
                NoteTypes = types.ToArray()
            };
        }

        private static void ParseTrackData(byte[] data, short ppq, ref float currentBpm, ref int lastPitch, List<float> timestamps, List<int> types)
        {
            int index = 0;
            long absoluteTicks = 0;
            byte runningStatus = 0;

            while (index < data.Length)
            {
                long deltaTimeTicks = ReadVariableLengthValue(data, ref index);
                absoluteTicks += deltaTimeTicks;

                if (index >= data.Length) break;

                byte statusByte = data[index];

                // Gerenciamento de Status Corrente (Running Status) do protocolo MIDI
                if ((statusByte & 0x80) != 0)
                {
                    runningStatus = statusByte;
                    index++;
                }

                // META EVENTOS DO SISTEMA (BPM, compasso, texto)
                if (runningStatus == 0xFF)
                {
                    byte metaType = data[index++];
                    long metaLength = ReadVariableLengthValue(data, ref index);

                    if (metaType == 0x51) // Meta-evento de mudança de tempo/BPM
                    {
                        int microsecondsPerBeat = (data[index] << 16) | (data[index + 1] << 8) | data[index + 2];
                        currentBpm = 60000000f / microsecondsPerBeat;
                    }
                    index += (int)metaLength;
                }
                // EVENTOS SYSEX (Ignorados no jogo de ritmo)
                else if (runningStatus == 0xF0 || runningStatus == 0xF7)
                {
                    long sysexLength = ReadVariableLengthValue(data, ref index);
                    index += (int)sysexLength;
                }
                // EVENTOS MUSICAIS DA TRACK
                else
                {
                    byte eventType = (byte)(runningStatus & 0xF0);

                    // Dentro do seu MidiTrackParser.cs -> método ParseTrackData -> no bloco eventType == 0x90

                    if (eventType == 0x90) // NOTE ON (Tecla pressionada no rolo de piano do MIDI)
                    {
                        int pitch = data[index++];
                        int velocity = data[index++];

                        if (velocity > 0) // Uma velocidade acima de zero indica um clique legítimo de nota
                        {
                            float timeInMs = (absoluteTicks * 60000f) / (currentBpm * ppq);
                            timestamps.Add(timeInMs);

                            int assignedType;

                            // -----------------------------------------------------------------
                            // DETECTOR ADAPTATIVO DE GHOST NOTES DO GUITAR PRO 8:
                            // ➔ Em vez de uma trava fixa, o algoritmo se adapta à dinâmica do GP8.
                            //    Notas fantasmas legítimas entre parênteses ( ) no GP8 exportam com 
                            //    uma redução massiva de Velocity. Se a nota estiver na faixa suave
                            //    de 1 a 45 de velocidade, ela é cravada como Fantasma (Tipo 4)!
                            // -----------------------------------------------------------------
                            if (velocity >= 1 && velocity <= 45)
                            {
                                assignedType = 4; // Nota Fantasma Legítima do Guitar Pro 8!
                                Debug.Log($"[MidiParser] ✨ GHOST NOTE detectada via GP8 Velocity: {velocity} no tempo {timeInMs:F0}ms");
                            }
                            else
                            {
                                // Se a Velocity for normal (acima de 45), segue a distribuição melódica por Pitch:
                                if (lastPitch == -1)
                                {
                                    assignedType = UnityEngine.Random.Range(0, 4); // Primeira nota da música
                                }
                                else
                                {
                                    int pitchDelta = pitch - lastPitch;

                                    if (pitchDelta == 0)
                                    {
                                        // Notas repetidas no GP8 têm chance de gerar Nota Errada/Obstáculo (Tipo 5)
                                        assignedType = (UnityEngine.Random.Range(0, 100) < 15) ? 5 : UnityEngine.Random.Range(0, 4);
                                    }
                                    else if (pitchDelta > 0)
                                    {
                                        if (pitchDelta > 4 && UnityEngine.Random.Range(0, 100) < 25) assignedType = 5; // Salto agudo violento = Obstáculo
                                        else assignedType = pitchDelta > 2 ? 3 : 2; // Distribui entre as NoteTracks 2 e 3
                                    }
                                    else
                                    {
                                        assignedType = pitchDelta < -2 ? 0 : 1; // Distribui entre as NoteTracks 0 e 1
                                    }
                                }

                                // Apenas atualizamos a memória de Pitch se NÃO for fantasma,
                                // mantendo a linha harmônica principal intocada para o jogador
                                lastPitch = pitch;
                            }

                            types.Add(assignedType);
                        }
                    }

                    else if (eventType == 0x80) // NOTE OFF (Tecla solta)
                    {
                        index += 2;
                    }
                    else if (eventType == 0xA0 || eventType == 0xB0 || eventType == 0xE0)
                    {
                        index += 2;
                    }
                    else if (eventType == 0xC0 || eventType == 0xD0)
                    {
                        index += 1;
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
