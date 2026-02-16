using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Ohana3DS_Rebirth.Ohana.Models;
using Ohana3DS_Rebirth.Ohana.Models.GenericFormats;
using Ohana3DS_Rebirth.Ohana.Models.PocketMonsters;
using Ohana3DS_Rebirth.Ohana.Textures.PocketMonsters;
using Ohana3DS_Rebirth.Ohana.Textures;
using Ohana3DS_Rebirth.Ohana.Compressions;
using Ohana3DS_Rebirth.Ohana.Containers;

namespace Ohana3DS_Rebirth.Ohana
{
    public class FileIO
    {
        [Flags]
        public enum formatType : uint
        {
            unsupported = 0,
            compression = 1 << 0,
            container = 1 << 1,
            image = 1 << 2,
            model = 1 << 3,
            texture = 1 << 4,
            anims = 1 << 5,
            all = 0xffffffff
        }

        public struct LoadedFile
        {
            public object data;
            public formatType type;
        }

        public static LoadedFile load(string fileName)
        {
            switch (Path.GetExtension(fileName).ToLower())
            {
                // TODO: format not yet ported
                // case ".mbn": return new LoadedFile { data = MBN.load(fileName), type = formatType.model };
                // case ".xml": return new LoadedFile { data = NLP.load(fileName), type = formatType.model };
                default: return load(new FileStream(fileName, FileMode.Open));
            }
        }

        public static LoadedFile load(Stream data)
        {
            //Too small
            if (data.Length < 0x10)
            {
                data.Close();
                return new LoadedFile { type = formatType.unsupported };
            }

            BinaryReader input = new BinaryReader(data);
            uint magic, length;

            switch (peek(input))
            {
                case 0x00010000: return new LoadedFile { data = GfModel.load(data), type = formatType.model };
                // TODO: animation support
                // case 0x00060000: return new LoadedFile { data = GfMotion.loadAnim(input), type = formatType.anims };
                case 0x15041213: return new LoadedFile { data = GfTexture.load(data), type = formatType.image };
                case 0x15122117:
                    RenderBase.OModelGroup mdls = new RenderBase.OModelGroup();
                    mdls.model.Add(GfModel.loadModel(data));
                    return new LoadedFile { data = mdls, type = formatType.model };
            }

            switch (getMagic(input, 7))
            {
                // TODO: format not yet ported
                // case "texture": return new LoadedFile { data = _3DST.load(data), type = formatType.image };
            }

            switch (getMagic(input, 5))
            {
                // TODO: format not yet ported
                // case "MODEL": return new LoadedFile { data = DQVIIPack.load(data), type = formatType.container };
            }

            switch (getMagic(input, 4))
            {
                // TODO: format not yet ported
                // case "CGFX": return new LoadedFile { data = CGFX.load(data), type = formatType.model };
                case "CRAG": return new LoadedFile { data = GARC.load(data), type = formatType.container };
                // TODO: format not yet ported
                // case "darc": return new LoadedFile { data = DARC.load(data), type = formatType.container };
                // case "FPT0": return new LoadedFile { data = FPT0.load(data), type = formatType.container };
                case "IECP":
                    magic = input.ReadUInt32();
                    length = input.ReadUInt32();
                    return load(new MemoryStream(LZSS.decompress(data, length)));
                // TODO: format not yet ported
                // case "NLK2":
                //     data.Seek(0x80, SeekOrigin.Begin);
                //     return new LoadedFile
                //     {
                //         data = CGFX.load(data),
                //         type = formatType.model
                //     };
                // case "SARC": return new LoadedFile { data = SARC.load(data), type = formatType.container };
                // case "SMES": return new LoadedFile { data = NLP.loadMesh(data), type = formatType.model };
                // TODO: format not yet ported (Yaz0 compression not available)
                // case "Yaz0":
                //     magic = input.ReadUInt32();
                //     length = IOUtils.endianSwap(input.ReadUInt32());
                //     data.Seek(8, SeekOrigin.Current);
                //     return load(new MemoryStream(Yaz0.decompress(data, length)));
                // case "zmdl": return new LoadedFile { data = ZMDL.load(data), type = formatType.model };
                // case "ztex": return new LoadedFile { data = ZTEX.load(data), type = formatType.texture };
            }

            // TODO: format not yet ported
            //Check if is a BCLIM or BFLIM file (header on the end)
            // if (data.Length > 0x28)
            // {
            //     data.Seek(-0x28, SeekOrigin.End);
            //     string clim = IOUtils.readStringWithLength(input, 4);
            //     if (clim == "CLIM" || clim == "FLIM") return new LoadedFile { data = BCLIM.load(data), type = formatType.image };
            // }

            switch (getMagic(input, 3))
            {
                case "BCH":
                    byte[] buffer = new byte[data.Length];
                    input.Read(buffer, 0, buffer.Length);
                    data.Close();
                    return new LoadedFile
                    {
                        data = BCH.load(new MemoryStream(buffer)),
                        type = formatType.model
                    };
                // TODO: format not yet ported
                // case "DMP": return new LoadedFile { data = DMP.load(data), type = formatType.image };
            }

            string magic2b = getMagic(input, 2);

            switch (magic2b)
            {
                case "AD": return new LoadedFile { data = AD.load(data), type = formatType.model };
                case "BM": return new LoadedFile { data = MM.load(data), type = formatType.model };
                // TODO: animation support
                // case "BS": return new LoadedFile { data = BS.load(data), type = formatType.anims };
                case "CM": return new LoadedFile { data = CM.load(data), type = formatType.model };
                case "CP": return new LoadedFile { data = CP.load(data), type = formatType.model };
                case "GR": return new LoadedFile { data = GR.load(data), type = formatType.model };
                case "MM": return new LoadedFile { data = MM.load(data), type = formatType.model };
                case "PC": return new LoadedFile { data = PC.load(data), type = formatType.model };
                case "PT": return new LoadedFile { data = PT.load(data), type = formatType.texture };
            }

            if (magic2b.Length == 2)
            {
                if ((magic2b[0] >= 'A' && magic2b[0] <= 'Z') &&
                    (magic2b[1] >= 'A' && magic2b[1] <= 'Z'))
                {
                    return new LoadedFile { data = PkmnContainer.load(data), type = formatType.container };
                }
            }

            //Compressions
            data.Seek(0, SeekOrigin.Begin);
            uint cmp = input.ReadUInt32();
            if ((cmp & 0xff) == 0x13) cmp = input.ReadUInt32();
            switch (cmp & 0xff)
            {
                case 0x11: return load(new MemoryStream(LZSS_Ninty.decompress(data, cmp >> 8)));
                case 0x90:
                    byte[] buffer = BLZ.decompress(data);
                    byte[] newData = new byte[buffer.Length - 1];
                    Buffer.BlockCopy(buffer, 1, newData, 0, newData.Length);
                    return load(new MemoryStream(newData));
            }

            data.Close();
            return new LoadedFile { type = formatType.unsupported };
        }

        public static string getExtension(byte[] data, int startIndex = 0)
        {
            if (data.Length > 3 + startIndex)
            {
                switch (getMagic(data, 4, startIndex))
                {
                    case "CGFX": return ".bcres";
                }
            }

            if (data.Length > 2 + startIndex)
            {
                switch (getMagic(data, 3, startIndex))
                {
                    case "BCH": return ".bch";
                }
            }

            if (data.Length > 1 + startIndex)
            {
                switch (getMagic(data, 2, startIndex))
                {
                    case "AD": return ".ad";
                    case "BG": return ".bg";
                    case "BM": return ".bm";
                    case "BS": return ".bs";
                    case "CM": return ".cm";
                    case "GR": return ".gr";
                    case "MM": return ".mm";
                    case "PB": return ".pb";
                    case "PC": return ".pc";
                    case "PF": return ".pf";
                    case "PK": return ".pk";
                    case "PO": return ".po";
                    case "PT": return ".pt";
                    case "TM": return ".tm";
                }
            }

            return ".bin";
        }

        private static uint peek(BinaryReader input)
        {
            uint value = input.ReadUInt32();
            input.BaseStream.Seek(-4, SeekOrigin.Current);
            return value;
        }

        private static string getMagic(BinaryReader input, uint length)
        {
            string magic = IOUtils.readString(input, 0, length);
            input.BaseStream.Seek(0, SeekOrigin.Begin);
            return magic;
        }

        public static string getMagic(byte[] data, int length, int startIndex = 0)
        {
            return Encoding.ASCII.GetString(data, startIndex, length);
        }

        public enum fileType
        {
            none,
            model,
            texture,
            skeletalAnimation,
            materialAnimation,
            visibilityAnimation
        }
    }
}
