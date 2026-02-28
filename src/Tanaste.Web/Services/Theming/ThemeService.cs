using MudBlazor;

namespace Tanaste.Web.Services.Theming;

/// <summary>
/// Manages the active UI theme.  Singleton: the MudTheme object is shared;
/// each circuit holds its own _isDark flag, synced via OnThemeChanged.
/// </summary>
public sealed class ThemeService
{
    /// <summary>Default to Dark Mode as specified in Section 1.2 of the UI ASD.</summary>
    public bool IsDarkMode { get; private set; } = true;

    /// <summary>
    /// The MudBlazor theme with Tanaste design tokens:
    ///   • 24 px corner radii on all components
    ///   • Deep-violet primary, teal secondary
    ///   • Dark palette tuned for a cinema-style media browser
    /// </summary>
    public MudTheme Theme { get; } = BuildTheme();

    /// <summary>Fired on every toggle so components can update their local _isDark field.</summary>
    public event Action? OnThemeChanged;

    public void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
        OnThemeChanged?.Invoke();
    }

    // ── Theme construction ────────────────────────────────────────────────────

    private static MudTheme BuildTheme() => new()
    {
        LayoutProperties = new LayoutProperties
        {
            // 24 px border radius required by Section 3 of the UI ASD.
            DefaultBorderRadius = "24px",
            DrawerWidthLeft     = "260px",
        },

        PaletteDark = new PaletteDark
        {
            Primary             = "#7C4DFF",   // deep violet
            PrimaryDarken       = "#5C35BF",
            PrimaryLighten      = "#9E6FFF",
            Secondary           = "#00BFA5",   // teal accent
            SecondaryDarken     = "#009688",
            Background          = "#0F0F14",
            BackgroundGray      = "#1A1A24",
            Surface             = "#1E1E2E",
            AppbarBackground    = "#13131D",
            DrawerBackground    = "#13131D",
            DrawerText          = "rgba(255,255,255,0.80)",
            DrawerIcon          = "rgba(255,255,255,0.60)",
            TextPrimary         = "rgba(255,255,255,0.87)",
            TextSecondary       = "rgba(255,255,255,0.60)",
            TextDisabled        = "rgba(255,255,255,0.38)",
            ActionDefault       = "rgba(255,255,255,0.54)",
            LinesDefault        = "rgba(255,255,255,0.12)",
            Divider             = "rgba(255,255,255,0.12)",
            OverlayDark         = "rgba(0,0,0,0.7)",
            Error               = "#CF6679",
            Warning             = "#FFB74D",
            Info                = "#4FC3F7",
            Success             = "#81C784",
        },

        PaletteLight = new PaletteLight
        {
            Primary    = "#5C35BF",
            Secondary  = "#00897B",
            Background = "#F4F4F8",
            Surface    = "#FFFFFF",
        },

        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = ["Roboto", "sans-serif"] },
            H4      = new H4Typography    { FontWeight   = "600" },
            H5      = new H5Typography    { FontWeight   = "600" },
            H6      = new H6Typography    { FontWeight   = "600" },
        },
    };
}
