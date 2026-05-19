// Copyright © Plain Concepts S.L.U. All rights reserved. Use is subject to license terms.

using Evergine.Common.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Common.Images
{
    /// <summary>
    /// This static class represents the image helpers.
    /// </summary>
    public static class ImageHelpers
    {
        /// <summary>
        /// Reads an Int16 from the binary reader.
        /// </summary>
        /// <param name="binaryReader">Binary reader.</param>
        /// <returns>Int16 data.</returns>
        public static short ReadLittleEndianInt16(BinaryReader binaryReader)
        {
            byte[] bytes = new byte[sizeof(short)];

            for (int i = 0; i < sizeof(short); i += 1)
            {
                bytes[sizeof(short) - 1 - i] = binaryReader.ReadByte();
            }

            return BitConverter.ToInt16(bytes, 0);
        }

        /// <summary>
        /// Reads a UInt16 from a binary reader.
        /// </summary>
        /// <param name="binaryReader">The binary reader.</param>
        /// <returns>The UInt16 data.</returns>
        public static ushort ReadLittleEndianUInt16(BinaryReader binaryReader)
        {
            byte[] bytes = new byte[sizeof(ushort)];

            for (int i = 0; i < sizeof(ushort); i += 1)
            {
                bytes[sizeof(ushort) - 1 - i] = binaryReader.ReadByte();
            }

            return BitConverter.ToUInt16(bytes, 0);
        }

        /// <summary>
        /// Reads an Int32 from the binary reader.
        /// </summary>
        /// <param name="binaryReader">The binary reader.</param>
        /// <returns>Int32 data.</returns>
        public static int ReadLittleEndianInt32(BinaryReader binaryReader)
        {
            byte[] bytes = new byte[sizeof(int)];
            for (int i = 0; i < sizeof(int); i += 1)
            {
                bytes[sizeof(int) - 1 - i] = binaryReader.ReadByte();
            }

            return BitConverter.ToInt32(bytes, 0);
        }

        /// <summary>
        /// Starts with.
        /// </summary>
        /// <param name="thisBytes">Source byte array.</param>
        /// <param name="thatBytes">Pattern byte array.</param>
        /// <returns>True if thisBytes starts with thatBytes.</returns>
        public static bool StartsWith(byte[] thisBytes, byte[] thatBytes)
        {
            for (int i = 0; i < thatBytes.Length; i += 1)
            {
                if (thisBytes[i] != thatBytes[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Converts BGRA to RGBA.
        /// </summary>
        /// <param name="bytes">The BGRA bytes.</param>
        /// <returns>The RGBA bytes.</returns>
        public static byte[] FromBGRA32ToRGBA32(ref byte[] bytes)
        {
            // BGRA to RGBA
            for (int k = 0; k < bytes.Length; k += 4)
            {
                var sourceBlue = bytes[k];
                bytes[k] = bytes[k + 2];
                bytes[k + 2] = sourceBlue;
            }

            return bytes;
        }

        /// <summary>
        /// Converts RGB to RGBA.
        /// </summary>
        /// <param name="bytes">The RGB bytes.</param>
        /// <param name="width">The image width.</param>
        /// <param name="height">The image height.</param>
        /// <returns>The RGBA bytes.</returns>
        public static byte[] FromRGB24ToRGBA32(byte[] bytes, int width, int height)
        {
            // RGB to RGBA
            byte[] rgba = new byte[width * height * 4];

            int index = 0;
            for (int i = 0; i < bytes.Length; i += 3)
            {
                rgba[index++] = bytes[i];
                rgba[index++] = bytes[i + 1];
                rgba[index++] = bytes[i + 2];
                rgba[index++] = 1;
            }

            return rgba;
        }

        /// <summary>
        /// Converts BGR to RGBA.
        /// </summary>
        /// <param name="bytes">The BGR bytes.</param>
        /// <param name="width">The image width.</param>
        /// <param name="height">The image height.</param>
        /// <returns>The RGBA bytes.</returns>
        public static byte[] FromBGR24ToRGBA32(byte[] bytes, int width, int height)
        {
            // BGR to RGBA
            byte[] rgba = new byte[width * height * 4];

            int index = 0;
            for (int i = 0; i < bytes.Length; i += 3)
            {
                rgba[index++] = bytes[i + 2];
                rgba[index++] = bytes[i + 1];
                rgba[index++] = bytes[i];
                rgba[index++] = 1;
            }

            return rgba;
        }

        /// <summary>
        /// Reads a struct from a binary reader.
        /// </summary>
        /// <typeparam name="T">The type to read.</typeparam>
        /// <param name="reader">The binary reader.</param>
        /// <returns>The read struct value.</returns>
        public static unsafe T ReadUnmanaged<T>(BinaryReader reader)
            where T : unmanaged
        {
            int size = Unsafe.SizeOf<T>();
            byte* bytes = stackalloc byte[size];

            for (int i = 0; i < size; i++)
            {
                bytes[i] = reader.ReadByte();
            }

            return Unsafe.Read<T>(bytes);
        }

        /// <summary>
        /// Writes an unmanaged type to a binary writer.
        /// </summary>
        /// <typeparam name="T">The type to write.</typeparam>
        /// <param name="writer">The binary writer.</param>
        /// <param name="value">The struct value to write.</param>
        public static unsafe void WriteUnmanaged<T>(BinaryWriter writer, T value)
            where T : unmanaged
        {
            int size = Unsafe.SizeOf<T>();
            var bytes = new ReadOnlySpan<byte>(&value, size);
            writer.Write(bytes);
        }

        /// <summary>
        /// Read texture description.
        /// </summary>
        /// <param name="reader">The binary reader.</param>
        /// <returns>The texture description.</returns>
        public static unsafe TextureDescription ReadTextureDescription(BinaryReader reader)
        {
            TextureType type = (TextureType)ReadUnmanaged<uint>(reader); // read uint for correct alignment: we used to serialize the TextureDescription struct as is. That means there was some padding for struct alignment, now we have to keep that for backwards compatibility
            PixelFormat format = ReadUnmanaged<PixelFormat>(reader);
            uint width = ReadUnmanaged<uint>(reader);
            uint height = ReadUnmanaged<uint>(reader);
            uint depth = ReadUnmanaged<uint>(reader);
            uint arraySize = ReadUnmanaged<uint>(reader);
            uint faces = ReadUnmanaged<uint>(reader); // deprecated, now we just use arraySize for everything
            uint mipLevels = ReadUnmanaged<uint>(reader);
            TextureFlags flags = ReadUnmanaged<TextureFlags>(reader);
            ResourceUsage usage = ReadUnmanaged<ResourceUsage>(reader);
            TextureSampleCount sampleCount = ReadUnmanaged<TextureSampleCount>(reader);
            ResourceCpuAccess cpuAccess = ReadUnmanaged<ResourceCpuAccess>(reader);
            return new TextureDescription
            {
                Type = type,
                Format = format,
                Width = width,
                Height = height,
                Depth = depth,
                ArraySize = arraySize * faces,
                MipLevels = mipLevels,
                Flags = flags,
                Usage = usage,
                SampleCount = sampleCount,
                CpuAccess = cpuAccess,
            };
        }

        /// <summary>
        /// Writes a texture description to a binary writer.
        /// </summary>
        /// <param name="writer">The binary writer.</param>
        /// <param name="description">The texture description.</param>
        public static unsafe void WriteTextureDescription(BinaryWriter writer, TextureDescription description)
        {
            WriteUnmanaged(writer, (uint)description.Type); // read uint for correct alignment
            WriteUnmanaged(writer, description.Format);
            WriteUnmanaged(writer, description.Width);
            WriteUnmanaged(writer, description.Height);
            WriteUnmanaged(writer, description.Depth);
            WriteUnmanaged(writer, description.ArraySize);
            WriteUnmanaged(writer, 1); // faces (deprecated)
            WriteUnmanaged(writer, description.MipLevels);
            WriteUnmanaged(writer, description.Flags);
            WriteUnmanaged(writer, description.Usage);
            WriteUnmanaged(writer, description.SampleCount);
            WriteUnmanaged(writer, description.CpuAccess);
        }

        public static byte[] GetImageArray(Image<Rgba32> image, bool premultiplyAlpha, out int dataLength)
        {
            var bytesPerPixel = image.PixelType.BitsPerPixel / 8;
            dataLength = image.Width * image.Height * bytesPerPixel;
            var data = new byte[dataLength];

            image.ProcessPixelRows(accessor =>
            {
                var dataPixels = MemoryMarshal.Cast<byte, Rgba32>(data.AsSpan());
                for (int i = 0; i < image.Height; i++)
                {
                    var row = accessor.GetRowSpan(i);
                    if (premultiplyAlpha)
                    {
                        CopyToPremultiplied(row, dataPixels.Slice(i * image.Width, image.Width));
                    }
                    else
                    {
                        row.CopyTo(dataPixels.Slice(i * image.Width, image.Width));
                    }
                }
            });

            return data;
        }

        private static void CopyToPremultiplied(ReadOnlySpan<Rgba32> pixels, Span<Rgba32> destination)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                Rgba32 pixel = pixels[i];
                ref Rgba32 destinationPixel = ref destination[i];
                byte a = pixel.A;
                if (a == 0)
                {
                    destinationPixel.PackedValue = 0;
                }
                else
                {
                    destinationPixel.R = (byte)((pixel.R * a) >> 8);
                    destinationPixel.G = (byte)((pixel.G * a) >> 8);
                    destinationPixel.B = (byte)((pixel.B * a) >> 8);
                    destinationPixel.A = pixel.A;
                }
            }
        }
    }
}
