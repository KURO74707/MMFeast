using UnityEngine;

public static class MenuFont
{
    public static Font Create()
    {
        // Use installed fonts without copying licensed OS font files into the project.
        string[] preferred = { "Microsoft YaHei", "Microsoft JhengHei", "PingFang SC", "Noto Sans CJK SC", "WenQuanYi Micro Hei" };
        var installed = Font.GetOSInstalledFontNames();
        foreach (string family in preferred)
            if (System.Array.IndexOf(installed, family) >= 0) return Font.CreateDynamicFontFromOSFont(family, 24);
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
