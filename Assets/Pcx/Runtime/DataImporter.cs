using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pcx
{
    public static class DataImporter
    {
        #region Internal data structure

        private enum DataFormat
        {
            Undefined = -1,
            Ascii,
            BinaryLittleEndian
        };

        private enum DataProperty
        {
            Invalid,
            R8,
            G8,
            B8,
            A8,
            R16,
            G16,
            B16,
            A16,
            R32,
            G32,
            B32,
            A32,
            SingleX,
            SingleY,
            SingleZ,
            DoubleX,
            DoubleY,
            DoubleZ,
            Data8,
            Data16,
            Data32,
            Data64
        }

        private static int GetPropertySize(DataProperty p)
        {
            switch (p)
            {
                case DataProperty.R8: return 1;
                case DataProperty.G8: return 1;
                case DataProperty.B8: return 1;
                case DataProperty.A8: return 1;
                case DataProperty.R16: return 2;
                case DataProperty.G16: return 2;
                case DataProperty.B16: return 2;
                case DataProperty.A16: return 2;
                case DataProperty.R32: return 4;
                case DataProperty.G32: return 4;
                case DataProperty.B32: return 4;
                case DataProperty.A32: return 4;
                case DataProperty.SingleX: return 4;
                case DataProperty.SingleY: return 4;
                case DataProperty.SingleZ: return 4;
                case DataProperty.DoubleX: return 8;
                case DataProperty.DoubleY: return 8;
                case DataProperty.DoubleZ: return 8;
                case DataProperty.Data8: return 1;
                case DataProperty.Data16: return 2;
                case DataProperty.Data32: return 4;
                case DataProperty.Data64: return 8;
            }

            return 0;
        }

        private class DataHeader
        {
            public DataFormat dataFormat = DataFormat.Undefined;
            public readonly List<DataProperty> properties = new List<DataProperty>();
            public int vertexCount = -1;
        }

        private class DataBody
        {
            public readonly List<Vector3> vertices;
            public readonly List<Color32> colors;

            public DataBody(int vertexCount)
            {
                vertices = new List<Vector3>(vertexCount);
                colors = new List<Color32>(vertexCount);
            }

            public void AddPoint(
                float x, float y, float z,
                byte r, byte g, byte b, byte a
            )
            {
                vertices.Add(new Vector3(x, y, z));
                colors.Add(new Color32(r, g, b, a));
            }
        }

        #endregion

        private static void ReadHeaderAndData(string path, out DataHeader header, out DataBody body)
        {
            var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);

            header = ReadDataHeader(new StreamReader(stream));

            body = header.dataFormat switch
            {
                DataFormat.BinaryLittleEndian => ReadDataBodyFormatBinaryLittleEndian(header, new BinaryReader(stream)),
                DataFormat.Ascii => ReadDataBodyFormatAscii(header, new StreamReader(stream)),
                _ => null
            };
        }

        public static async UniTask<Mesh> ImportAsMesh(string path)
        {
            try
            {
                ReadHeaderAndData(path, out var header, out var body);

                var mesh = new Mesh
                {
                    name = Path.GetFileNameWithoutExtension(path),
                    indexFormat = header.vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
                };

                mesh.SetVertices(body.vertices);
                mesh.SetColors(body.colors);

                mesh.SetIndices(
                    Enumerable.Range(0, header.vertexCount).ToArray(),
                    MeshTopology.Points, 0
                );

                mesh.UploadMeshData(true);
                return mesh;
            }
            catch (Exception e)
            {
                Debug.LogError("Failed importing " + path + ". " + e.Message);
                return null;
            }
        }

        public static async UniTask<PointCloudData> ImportAsPointCloudData(string path)
        {
            try
            {
                await UniTask.SwitchToThreadPool();
                ReadHeaderAndData(path, out var header, out var body);
                await UniTask.SwitchToMainThread();
                var data = ScriptableObject.CreateInstance<PointCloudData>();
                data.Initialize(body.vertices, body.colors);
                data.name = Path.GetFileNameWithoutExtension(path);
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError("Failed importing " + path + ". " + e.Message);
                return null;
            }
        }

        public static async UniTask<BakedPointCloud> ImportAsBakedPointCloud(string path)
        {
            try
            {
                ReadHeaderAndData(path, out var header, out var body);
                var data = ScriptableObject.CreateInstance<BakedPointCloud>();
                data.Initialize(body.vertices, body.colors);
                data.name = Path.GetFileNameWithoutExtension(path);
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError("Failed importing " + path + ". " + e.Message);
                return null;
            }
        }

        private static DataHeader ReadDataHeader(StreamReader reader)
        {
            var data = new DataHeader();
            var readCount = 0;

            var line = reader.ReadLine();
            readCount += line!.Length + 1;
            if (line != "ply")
                throw new ArgumentException("Magic number ('ply') mismatch.");

            line = reader.ReadLine();
            readCount += line!.Length + 1;


            switch (line)
            {
                case "format binary_little_endian 1.0":
                    data.dataFormat = DataFormat.BinaryLittleEndian;
                    break;

                case "format ascii 1.0":
                    data.dataFormat = DataFormat.Ascii;
                    break;

                default:
                    throw new ArgumentException($"Invalid data format ('{line}'). Should be binary(little endian) or ASCII");
            }

            // Read header contents.
            for (var skip = false;;)
            {
                // Read a line and split it with white space.
                line = reader.ReadLine();
                readCount += line.Length + 1;
                if (line == "end_header") break;
                var col = line.ToLower().Split();

                switch (col[0])
                {
                    // Element declaration (unskippable)
                    case "comment":
                        skip = true;
                        break;
                    case "element" when col[1] == "vertex":
                        data.vertexCount = Convert.ToInt32(col[2]);
                        skip = false;
                        break;
                    case "element":
                        // Don't read elements other than vertices.
                        skip = true;
                        break;
                }

                if (skip) continue;

                // Property declaration line
                if (col[0] == "property")
                {
                    // Parse the property name entry.
                    var prop = col[2] switch
                    {
                        "red" => DataProperty.R8,
                        "green" => DataProperty.G8,
                        "blue" => DataProperty.B8,
                        "alpha" => DataProperty.A8,
                        "x" => DataProperty.SingleX,
                        "y" => DataProperty.SingleY,
                        "z" => DataProperty.SingleZ,
                        _ => DataProperty.Invalid
                    };

                    switch (col[1])
                    {
                        // Check the property type.
                        case "char":
                        case "uchar":
                        case "int8":
                        case "uint8":
                        {
                            if (prop == DataProperty.Invalid)
                                prop = DataProperty.Data8;
                            else if (GetPropertySize(prop) != 1)
                                throw new ArgumentException("Invalid property type ('" + line + "').");
                            break;
                        }
                        case "short":
                        case "ushort":
                        case "int16":
                        case "uint16":
                        {
                            prop = prop switch
                            {
                                DataProperty.Invalid => DataProperty.Data16,
                                DataProperty.R8 => DataProperty.R16,
                                DataProperty.G8 => DataProperty.G16,
                                DataProperty.B8 => DataProperty.B16,
                                DataProperty.A8 => DataProperty.A16,
                                _ => prop
                            };

                            if (GetPropertySize(prop) != 2)
                                throw new ArgumentException("Invalid property type ('" + line + "').");
                            break;
                        }
                        case "int":
                        case "uint":
                        case "float":
                        case "int32":
                        case "uint32":
                        case "float32":
                        {
                            prop = prop switch
                            {
                                DataProperty.Invalid => DataProperty.Data32,
                                DataProperty.R8 => DataProperty.R32,
                                DataProperty.G8 => DataProperty.G32,
                                DataProperty.B8 => DataProperty.B32,
                                DataProperty.A8 => DataProperty.A32,
                                _ => prop
                            };

                            if (GetPropertySize(prop) != 4)
                                throw new ArgumentException("Invalid property type ('" + line + "').");
                            break;
                        }
                        case "int64":
                        case "uint64":
                        case "double":
                        case "float64":
                        {
                            prop = prop switch
                            {
                                DataProperty.Invalid => DataProperty.Data64,
                                DataProperty.SingleX => DataProperty.DoubleX,
                                DataProperty.SingleY => DataProperty.DoubleY,
                                DataProperty.SingleZ => DataProperty.DoubleZ,
                                _ => prop
                            };

                            if (GetPropertySize(prop) != 8)
                                throw new ArgumentException("Invalid property type ('" + line + "').");
                            break;
                        }
                        default:
                            throw new ArgumentException("Unsupported property type ('" + line + "').");
                    }

                    data.properties.Add(prop);
                }
            }

            // Rewind the stream back to the exact position of the reader.
            reader.BaseStream.Position = data.dataFormat == DataFormat.BinaryLittleEndian ? readCount : 0;

            return data;
        }

        private static DataBody ReadDataBodyFormatBinaryLittleEndian(DataHeader header, BinaryReader reader)
        {
            var data = new DataBody(header.vertexCount);

            // await UniTask.SwitchToThreadPool();

            float x = 0, y = 0, z = 0;
            byte r = 255, g = 255, b = 255, a = 255;

            for (var i = 0; i < header.vertexCount; i++)
            {
                foreach (var prop in header.properties)
                {
                    switch (prop)
                    {
                        case DataProperty.R8:
                            r = reader.ReadByte();
                            break;
                        case DataProperty.G8:
                            g = reader.ReadByte();
                            break;
                        case DataProperty.B8:
                            b = reader.ReadByte();
                            break;
                        case DataProperty.A8:
                            a = reader.ReadByte();
                            break;
                        case DataProperty.R32:
                            r = (byte)(reader.ReadSingle() * 255.0f);
                            break;
                        case DataProperty.G32:
                            g = (byte)(reader.ReadSingle() * 255.0f);
                            break;
                        case DataProperty.B32:
                            b = (byte)(reader.ReadSingle() * 255.0f);
                            break;
                        case DataProperty.A32:
                            a = (byte)(reader.ReadSingle() * 255.0f);
                            break;

                        case DataProperty.R16:
                            r = (byte)(reader.ReadUInt16() >> 8);
                            break;
                        case DataProperty.G16:
                            g = (byte)(reader.ReadUInt16() >> 8);
                            break;
                        case DataProperty.B16:
                            b = (byte)(reader.ReadUInt16() >> 8);
                            break;
                        case DataProperty.A16:
                            a = (byte)(reader.ReadUInt16() >> 8);
                            break;

                        case DataProperty.SingleX:
                            x = reader.ReadSingle();
                            break;
                        case DataProperty.SingleY:
                            y = reader.ReadSingle();
                            break;
                        case DataProperty.SingleZ:
                            z = reader.ReadSingle();
                            break;

                        case DataProperty.DoubleX:
                            x = (float)reader.ReadDouble();
                            break;
                        case DataProperty.DoubleY:
                            y = (float)reader.ReadDouble();
                            break;
                        case DataProperty.DoubleZ:
                            z = (float)reader.ReadDouble();
                            break;

                        case DataProperty.Data8:
                            reader.ReadByte();
                            break;
                        case DataProperty.Data16:
                            reader.BaseStream.Position += 2;
                            break;
                        case DataProperty.Data32:
                            reader.BaseStream.Position += 4;
                            break;
                        case DataProperty.Data64:
                            reader.BaseStream.Position += 8;
                            break;
                        case DataProperty.Invalid:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                data.AddPoint(x, y, z, r, g, b, a);
            }

            // await UniTask.SwitchToMainThread();

            return data;
        }

        private static DataBody ReadDataBodyFormatAscii(DataHeader header, TextReader reader)
        {
            var data = new DataBody(header.vertexCount);

            float x = 0, y = 0, z = 0;
            byte r = 255, g = 255, b = 255, a = 255;

            var line = reader.ReadLine();

            // Skip header
            while (line != "end_header")
            {
                line = reader.ReadLine();
            }

            // Parse data according to properties list
            var propertiesCount = header.properties.Count;

            var ci = (CultureInfo)CultureInfo.CurrentCulture.Clone();
            ci.NumberFormat.NumberDecimalSeparator = ".";

            for (var i = 0; i < header.vertexCount; ++i)
            {
                line = reader.ReadLine();
                var values = line.Split();

                for (var j = 0; j < propertiesCount; ++j)
                {
                    var prop = header.properties[j];
                    var value = values[j];

                    switch (prop)
                    {
                        case DataProperty.R8:
                            r = byte.Parse(value);
                            break;
                        case DataProperty.G8:
                            g = byte.Parse(value);
                            break;
                        case DataProperty.B8:
                            b = byte.Parse(value);
                            break;
                        case DataProperty.A8:
                            a = byte.Parse(value);
                            break;

                        case DataProperty.R16:
                            r = (byte)(ushort.Parse(value) >> 8);
                            break;
                        case DataProperty.G16:
                            g = (byte)(ushort.Parse(value) >> 8);
                            break;
                        case DataProperty.B16:
                            b = (byte)(ushort.Parse(value) >> 8);
                            break;
                        case DataProperty.A16:
                            a = (byte)(ushort.Parse(value) >> 8);
                            break;

                        case DataProperty.R32:
                            r = (byte)(float.Parse(value, NumberStyles.Float, ci) * 255.0f);
                            break;
                        case DataProperty.G32:
                            g = (byte)(float.Parse(value, NumberStyles.Float, ci) * 255.0f);
                            break;
                        case DataProperty.B32:
                            b = (byte)(float.Parse(value, NumberStyles.Float, ci) * 255.0f);
                            break;
                        case DataProperty.A32:
                            a = (byte)(float.Parse(value, NumberStyles.Float, ci) * 255.0f);
                            break;

                        case DataProperty.SingleX:
                            x = float.Parse(value, NumberStyles.Float, ci);
                            break;
                        case DataProperty.SingleY:
                            y = float.Parse(value, NumberStyles.Float, ci);
                            break;
                        case DataProperty.SingleZ:
                            z = float.Parse(value, NumberStyles.Float, ci);
                            break;

                        case DataProperty.DoubleX:
                            x = (float)double.Parse(value, NumberStyles.Float, ci);
                            break;
                        case DataProperty.DoubleY:
                            y = (float)double.Parse(value, NumberStyles.Float, ci);
                            break;
                        case DataProperty.DoubleZ:
                            z = (float)double.Parse(value, NumberStyles.Float, ci);
                            break;
                        case DataProperty.Invalid:
                            break;
                        case DataProperty.Data8:
                            break;
                        case DataProperty.Data16:
                            break;
                        case DataProperty.Data32:
                            break;
                        case DataProperty.Data64:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                data.AddPoint(x, y, z, r, g, b, a);
            }

            return data;
        }
    }
}