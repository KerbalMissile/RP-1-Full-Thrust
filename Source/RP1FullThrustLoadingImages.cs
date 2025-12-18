using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

[KSPAddon(KSPAddon.Startup.Instantly, true)]
public class RP1FullThrustLoadingImages : MonoBehaviour
{
    private const string ROLoadingImagesDir = "GameData/ROLoadingImages/PluginData/Screens";
    private const string RP1FullThrustDir = "GameData/RP-1FullThrust/LoadingScreens";
    private const int MainLoadingScreenIdx = 3;

    private bool _done = false;

    void Awake()
    {
        DontDestroyOnLoad(this);
    }

    void Update()
    {
        if (_done || LoadingScreen.Instance == null)
            return;

        if (LoadingScreen.Instance.Screens == null)
            return;

        if (LoadingScreen.Instance.Screens.Count <= MainLoadingScreenIdx)
            return;

        // Only proceed if ROLoadingImages has (likely) run - detect by type presence
        var roType = Type.GetType("ROLoadingImages.LoadingImageReplacer, ROLoadingImages");
        if (roType == null)
        {
            Debug.Log("[RP1FullThrust] ROLoadingImages not present yet - waiting.");
            return;
        }

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Debug.Log("[RP1FullThrust] Combining default loading screens with RP-1 Full Thrust screens.");

            LoadingScreen.LoadingScreenState sc = LoadingScreen.Instance.Screens[MainLoadingScreenIdx];

            // Start with existing textures already in the loading screen (preserve defaults)
            List<Texture2D> textures = new List<Texture2D>();
            HashSet<string> namesSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (sc.screens != null)
            {
                foreach (var obj in sc.screens)
                {
                    Texture2D tex = obj as Texture2D;
                    if (tex != null)
                    {
                        textures.Add(tex);
                        if (!string.IsNullOrEmpty(tex.name))
                            namesSeen.Add(tex.name);
                        Debug.Log(string.Format("[RP1FullThrust] Preserved existing texture: {0}", tex.name ?? "(unnamed)"));
                    }
                }
            }
            else
            {
                Debug.Log("[RP1FullThrust] No existing screens found on the main loading screen state.");
            }

            // Load DDS from ROLoadingImages directory
            string roDirFull = KSPUtil.ApplicationRootPath + ROLoadingImagesDir;
            LoadDDSFromDirectory(textures, namesSeen, roDirFull, "RO_");

            // Load DDS (and other images optionally) from RP-1 Full Thrust directory
            string rp1DirFull = KSPUtil.ApplicationRootPath + RP1FullThrustDir;
            LoadDDSFromDirectory(textures, namesSeen, rp1DirFull, "RP1FT_");

            if (textures.Count > 0)
            {
                sc.screens = textures.ToArray();
                sc.displayTime = Math.Max(sc.displayTime, 6f); // ensure reasonable display time
                Debug.Log(string.Format("[RP1FullThrust] Combined loading screens set. Total images: {0}. Elapsed: {1} ms.", textures.Count, sw.ElapsedMilliseconds));
            }
            else
            {
                Debug.LogError("[RP1FullThrust] No textures loaded from either directory and none were present originally.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError(string.Format("[RP1FullThrust] Exception while combining loading screens: {0}", ex));
        }

        // Clean up - we only need to run once
        try
        {
            Destroy(this);
        }
        catch { }
        _done = true;
    }

    private void LoadDDSFromDirectory(List<Texture2D> textures, HashSet<string> namesSeen, string directoryFullPath, string namePrefix)
    {
        try
        {
            if (!Directory.Exists(directoryFullPath))
            {
                Debug.Log(string.Format("[RP1FullThrust] Directory not found: {0}", directoryFullPath));
                return;
            }

            DirectoryInfo di = new DirectoryInfo(directoryFullPath);
            FileInfo[] files = di.GetFiles();
            Debug.Log(string.Format("[RP1FullThrust] Scanning {0} for DDS files (found {1} entries).", directoryFullPath, files.Length));

            foreach (FileInfo fi in files)
            {
                string ext = fi.Extension;
                if (!ext.Equals(".dds", StringComparison.OrdinalIgnoreCase))
                    continue;

                string texName = namePrefix + fi.Name;
                if (namesSeen.Contains(texName))
                {
                    Debug.Log(string.Format("[RP1FullThrust] Skipping duplicate image name: {0}", texName));
                    continue;
                }

                try
                {
                    Texture2D t = LoadDDS(fi.FullName);
                    if (t != null)
                    {
                        // give it a deterministic name to detect duplicates later
                        try { t.name = texName; } catch { }
                        textures.Add(t);
                        namesSeen.Add(texName);
                        Debug.Log(string.Format("[RP1FullThrust] Loaded DDS: {0}", fi.FullName));
                    }
                    else
                    {
                        Debug.Log(string.Format("[RP1FullThrust] LoadDDS returned null for: {0}", fi.FullName));
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError(string.Format("[RP1FullThrust] Exception loading DDS {0}: {1}", fi.FullName, ex));
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError(string.Format("[RP1FullThrust] Error scanning directory {0}: {1}", directoryFullPath, ex));
        }
    }

    // --- DDS Loader (Sarbian) ---
    private const uint DDSD_MIPMAPCOUNT_BIT = 0x00020000;
    private const uint DDPF_ALPHAPIXELS = 0x00001;
    private const uint DDPF_ALPHA = 0x00002;
    private const uint DDPF_FOURCC = 0x00004;
    private const uint DDPF_RGB = 0x000040;
    private const uint DDPF_YUV = 0x0000200;
    private const uint DDPF_LUMINANCE = 0x00020000;
    private const uint DDPF_NORMAL = 0x80000;

    private static string error;

    public static Texture2D LoadDDS(string path)
    {
        if (!File.Exists(path))
        {
            error = "File does not exist";
            return null;
        }

        using (BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read)))
        {
            byte[] dwMagic = reader.ReadBytes(4);
            if (!fourCCEquals(dwMagic, "DDS "))
            {
                error = "Invalid DDS file";
                return null;
            }

            int dwSize = (int)reader.ReadUInt32();
            if (dwSize != 124)
            {
                error = "Invalid header size";
                return null;
            }

            int dwFlags = (int)reader.ReadUInt32();
            int dwHeight = (int)reader.ReadUInt32();
            int dwWidth = (int)reader.ReadUInt32();

            int dwPitchOrLinearSize = (int)reader.ReadUInt32();
            int dwDepth = (int)reader.ReadUInt32();
            int dwMipMapCount = (int)reader.ReadUInt32();

            if ((dwFlags & DDSD_MIPMAPCOUNT_BIT) == 0)
            {
                dwMipMapCount = 1;
            }

            for (int i = 0; i < 11; i++)
                reader.ReadUInt32();

            uint dds_pxlf_dwSize = reader.ReadUInt32();
            uint dds_pxlf_dwFlags = reader.ReadUInt32();
            byte[] dds_pxlf_dwFourCC = reader.ReadBytes(4);
            string fourCC = Encoding.ASCII.GetString(dds_pxlf_dwFourCC);
            uint dds_pxlf_dwRGBBitCount = reader.ReadUInt32();
            uint pixelSize = dds_pxlf_dwRGBBitCount / 8;
            uint dds_pxlf_dwRBitMask = reader.ReadUInt32();
            uint dds_pxlf_dwGBitMask = reader.ReadUInt32();
            uint dds_pxlf_dwBBitMask = reader.ReadUInt32();
            uint dds_pxlf_dwABitMask = reader.ReadUInt32();

            int dwCaps = (int)reader.ReadUInt32();
            int dwCaps2 = (int)reader.ReadUInt32();
            int dwCaps3 = (int)reader.ReadUInt32();
            int dwCaps4 = (int)reader.ReadUInt32();
            int dwReserved2 = (int)reader.ReadUInt32();

            TextureFormat textureFormat = TextureFormat.ARGB32;
            bool isCompressed = false;
            bool isNormalMap = (dds_pxlf_dwFlags & DDPF_NORMAL) != 0;

            bool alpha = (dds_pxlf_dwFlags & DDPF_ALPHA) != 0;
            bool fourcc = (dds_pxlf_dwFlags & DDPF_FOURCC) != 0;
            bool rgb = (dds_pxlf_dwFlags & DDPF_RGB) != 0;
            bool alphapixel = (dds_pxlf_dwFlags & DDPF_ALPHAPIXELS) != 0;
            bool luminance = (dds_pxlf_dwFlags & DDPF_LUMINANCE) != 0;
            bool rgb888 = dds_pxlf_dwRBitMask == 0x0000ff && dds_pxlf_dwGBitMask == 0x0000ff00 && dds_pxlf_dwBBitMask == 0x00ff0000;
            bool bgr888 = dds_pxlf_dwRBitMask == 0x00ff0000 && dds_pxlf_dwGBitMask == 0x0000ff00 && dds_pxlf_dwBBitMask == 0x0000ff;
            bool rgb565 = dds_pxlf_dwRBitMask == 0x0000F800 && dds_pxlf_dwGBitMask == 0x00007E0 && dds_pxlf_dwBBitMask == 0x00001F;
            bool argb4444 = dds_pxlf_dwABitMask == 0x0000f000 && dds_pxlf_dwRBitMask == 0x0000f00 && dds_pxlf_dwGBitMask == 0x0000f0 && dds_pxlf_dwBBitMask == 0x0000f;
            bool rbga4444 = dds_pxlf_dwABitMask == 0x0000f && dds_pxlf_dwRBitMask == 0x0000f000 && dds_pxlf_dwGBitMask == 0x0000f0 && dds_pxlf_dwBBitMask == 0x0000f00;

            if (fourcc)
            {
                isCompressed = true;

                if (fourCCEquals(dds_pxlf_dwFourCC, "DXT1"))
                    textureFormat = TextureFormat.DXT1;
                else if (fourCCEquals(dds_pxlf_dwFourCC, "DXT5"))
                    textureFormat = TextureFormat.DXT5;
            }
            else if (rgb && (rgb888 || bgr888))
            {
                textureFormat = alphapixel ? TextureFormat.RGBA32 : TextureFormat.RGB24;
            }
            else if (rgb && rgb565)
            {
                textureFormat = TextureFormat.RGB565;
            }
            else if (rgb && alphapixel && argb4444)
            {
                textureFormat = TextureFormat.ARGB4444;
            }
            else if (rgb && alphapixel && rbga4444)
            {
                textureFormat = TextureFormat.RGBA4444;
            }
            else if (!rgb && alpha != luminance)
            {
                textureFormat = TextureFormat.Alpha8;
            }
            else
            {
                error = "Unsupported DDS format";
                return null;
            }

            long dataBias = 128;
            long dxtBytesLength = reader.BaseStream.Length - dataBias;
            reader.BaseStream.Seek(dataBias, SeekOrigin.Begin);
            byte[] dxtBytes = reader.ReadBytes((int)dxtBytesLength);

            if (!isCompressed && bgr888)
            {
                for (uint i = 0; i < dxtBytes.Length; i += pixelSize)
                {
                    byte b = dxtBytes[i + 0];
                    byte r = dxtBytes[i + 2];
                    dxtBytes[i + 0] = r;
                    dxtBytes[i + 2] = b;
                }
            }

            Texture2D texture = new Texture2D(dwWidth, dwHeight, textureFormat, dwMipMapCount > 1);
            texture.LoadRawTextureData(dxtBytes);
            texture.name = Path.GetFileName(path);
            texture.Apply(false, true);

            return texture;
        }
    }

    private static bool fourCCEquals(IList<byte> bytes, string s)
    {
        return bytes[0] == s[0] && bytes[1] == s[1] && bytes[2] == s[2] && bytes[3] == s[3];
    }
}