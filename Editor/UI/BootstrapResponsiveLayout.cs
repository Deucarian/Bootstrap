using System;

namespace Deucarian.Bootstrap.Editor
{
    internal enum BootstrapResponsiveMode
    {
        Narrow,
        Compact,
        Wide
    }

    /// <summary>
    /// Pure width-based layout policy for the Bootstrap setup surface.
    /// The view translates <see cref="ClassName"/> into USS classes; this type has no
    /// dependency on EditorWindow, UI Toolkit, or other Unity static APIs.
    /// </summary>
    internal static class BootstrapResponsiveLayout
    {
        public const float NarrowBreakpoint = 900f;
        public const float WideBreakpoint = 1180f;

        public const string NarrowClassName = "bootstrap-responsive--narrow";
        public const string CompactClassName = "bootstrap-responsive--compact";
        public const string WideClassName = "bootstrap-responsive--wide";

        public static BootstrapResponsiveMode ResolveMode(float windowWidth)
        {
            float safeWidth = SanitizeDimension(windowWidth);

            if (safeWidth < NarrowBreakpoint)
            {
                return BootstrapResponsiveMode.Narrow;
            }

            return safeWidth < WideBreakpoint
                ? BootstrapResponsiveMode.Compact
                : BootstrapResponsiveMode.Wide;
        }

        public static BootstrapResponsiveLayoutState Calculate(float windowWidth)
        {
            return Calculate(windowWidth, 0f);
        }

        public static BootstrapResponsiveLayoutState Calculate(float windowWidth, float windowHeight)
        {
            float safeWidth = SanitizeDimension(windowWidth);
            float safeHeight = SanitizeDimension(windowHeight);
            BootstrapResponsiveMode mode = ResolveMode(safeWidth);

            switch (mode)
            {
                case BootstrapResponsiveMode.Wide:
                    return new BootstrapResponsiveLayoutState(
                        mode,
                        WideClassName,
                        safeWidth,
                        safeHeight,
                        contentPadding: 24f,
                        sectionGap: 16f,
                        stepColumns: 3,
                        headerStacked: false,
                        actionsStacked: false,
                        primaryActionFillsRow: false,
                        actionBarMinimumHeight: 58f);

                case BootstrapResponsiveMode.Compact:
                    return new BootstrapResponsiveLayoutState(
                        mode,
                        CompactClassName,
                        safeWidth,
                        safeHeight,
                        contentPadding: 16f,
                        sectionGap: 12f,
                        stepColumns: 2,
                        headerStacked: false,
                        actionsStacked: false,
                        primaryActionFillsRow: false,
                        actionBarMinimumHeight: 58f);

                default:
                    return new BootstrapResponsiveLayoutState(
                        mode,
                        NarrowClassName,
                        safeWidth,
                        safeHeight,
                        contentPadding: 12f,
                        sectionGap: 12f,
                        stepColumns: 1,
                        headerStacked: false,
                        actionsStacked: false,
                        primaryActionFillsRow: true,
                        actionBarMinimumHeight: 58f);
            }
        }

        private static float SanitizeDimension(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : Math.Max(0f, value);
        }
    }

    /// <summary>
    /// Immutable decisions consumed by the Bootstrap presentation layer.
    /// </summary>
    internal readonly struct BootstrapResponsiveLayoutState
    {
        public BootstrapResponsiveLayoutState(
            BootstrapResponsiveMode mode,
            string className,
            float windowWidth,
            float windowHeight,
            float contentPadding,
            float sectionGap,
            int stepColumns,
            bool headerStacked,
            bool actionsStacked,
            bool primaryActionFillsRow,
            float actionBarMinimumHeight)
        {
            Mode = mode;
            ClassName = className ?? string.Empty;
            WindowWidth = windowWidth;
            WindowHeight = windowHeight;
            ContentPadding = contentPadding;
            SectionGap = sectionGap;
            StepColumns = stepColumns;
            HeaderStacked = headerStacked;
            ActionsStacked = actionsStacked;
            PrimaryActionFillsRow = primaryActionFillsRow;
            ActionBarMinimumHeight = actionBarMinimumHeight;
            AvailableBodyHeight = Math.Max(0f, windowHeight - actionBarMinimumHeight);
        }

        public BootstrapResponsiveMode Mode { get; }

        public string ClassName { get; }

        public float WindowWidth { get; }

        public float WindowHeight { get; }

        public float ContentPadding { get; }

        public float SectionGap { get; }

        public int StepColumns { get; }

        public bool HeaderStacked { get; }

        public bool ActionsStacked { get; }

        public bool PrimaryActionFillsRow { get; }

        public float ActionBarMinimumHeight { get; }

        public float AvailableBodyHeight { get; }

        public bool IsNarrow
        {
            get { return Mode == BootstrapResponsiveMode.Narrow; }
        }

        public bool IsCompact
        {
            get { return Mode == BootstrapResponsiveMode.Compact; }
        }

        public bool IsWide
        {
            get { return Mode == BootstrapResponsiveMode.Wide; }
        }
    }
}
