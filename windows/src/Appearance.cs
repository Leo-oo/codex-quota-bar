using System;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

internal sealed class Appearance
{
    internal bool Dark;
    internal Color Surface, Ink, Muted, Border, Rail, Secondary;
    internal static Appearance Palette(bool dark)
    {
        return new Appearance { Dark=dark,
            Surface=dark?Color.FromArgb(40,40,40):Color.White,
            Ink=dark?Color.FromArgb(236,236,236):Color.FromArgb(36,36,36),
            Muted=dark?Color.FromArgb(185,169,141):Color.FromArgb(139,113,73),
            Border=dark?Color.FromArgb(68,68,68):Color.FromArgb(224,224,224),
            Rail=dark?Color.FromArgb(32,33,35):Color.FromArgb(240,239,245),
            Secondary=dark?Color.FromArgb(161,161,161):Color.FromArgb(130,130,130) };
    }
    internal static bool? ParseTheme(string text)
    {
        // Current client stores this under [desktop]; support legacy root settings too.
        string rootTheme=null,desktopTheme=null;
        int section=0;
        using(var reader=new StringReader(text))
        {
            string line;
            while((line=reader.ReadLine())!=null)
            {
                if(line.TrimStart().StartsWith("["))
                {
                    section=Regex.IsMatch(line,"^\\s*\\[\\s*(?:desktop|\"desktop\"|'desktop')\\s*\\]\\s*(?:#.*)?$",RegexOptions.IgnoreCase)?1:2;
                    continue;
                }
                if(section==2) continue;
                var match=Regex.Match(line,"^\\s*appearanceTheme\\s*=\\s*[\"'](light|dark|system)[\"']\\s*(?:#.*)?$",RegexOptions.IgnoreCase);
                if(match.Success)
                {
                    if(section==1) desktopTheme=match.Groups[1].Value;
                    else rootTheme=match.Groups[1].Value;
                }
            }
        }
        string theme=desktopTheme??rootTheme;
        return String.Equals(theme,"dark",StringComparison.OrdinalIgnoreCase)?true:
            String.Equals(theme,"light",StringComparison.OrdinalIgnoreCase)?false:(bool?)null;
    }
    internal static Appearance Read(string path, Appearance previous)
    {
        try
        {
            bool? dark=File.Exists(path)?ParseTheme(File.ReadAllText(path)):null;
            if(!dark.HasValue)
            {
                using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",false))
                {
                    object light=key==null?null:key.GetValue("AppsUseLightTheme");
                    dark=light is int?(int)light==0:false;
                }
            }
            return Palette(dark.Value);
        }
        catch(IOException) { return previous??Palette(false); }
        catch(UnauthorizedAccessException) { return previous??Palette(false); }
        catch(System.Security.SecurityException) { return previous??Palette(false); }
    }
    internal static string ConfigPath
    {
        get
        {
            string root=Environment.GetEnvironmentVariable("CODEX_HOME");
            if(String.IsNullOrWhiteSpace(root)) root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".codex");
            return Path.Combine(root,"config.toml");
        }
    }
}
