using System.Windows;
using System.Windows.Controls;

namespace ClaudeCodePanel.Windows.Views.Shared
{
    /// <summary>
    /// A section header control matching the Claude-Win SectionHeader design:
    /// Large bold title + description subtitle.
    /// </summary>
    public class SectionHeader : Control
    {
        static SectionHeader()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(SectionHeader),
                new FrameworkPropertyMetadata(typeof(SectionHeader)));
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(
                nameof(Title),
                typeof(string),
                typeof(SectionHeader),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(
                nameof(Description),
                typeof(string),
                typeof(SectionHeader),
                new PropertyMetadata(string.Empty));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }
    }

    /// <summary>
    /// A thin semi-transparent horizontal divider matching the Swift GlassDivider:
    /// Rectangle with white at 5% opacity, height 0.5px, and 10px vertical margin.
    /// Uses the theme's BorderDividerBrush (#0CFFFFFF).
    /// </summary>
    public class GlassDivider : Control
    {
        static GlassDivider()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(GlassDivider),
                new FrameworkPropertyMetadata(typeof(GlassDivider)));
        }
    }

    /// <summary>
    /// A centered empty-state placeholder matching the Swift EmptyState design:
    /// large secondary-colored icon (40pt) above a secondary-colored message,
    /// multiline-centered, with 40px padding and 15px spacing between elements.
    /// </summary>
    public class EmptyState : Control
    {
        static EmptyState()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(EmptyState),
                new FrameworkPropertyMetadata(typeof(EmptyState)));
        }

        public static readonly DependencyProperty IconGlyphProperty =
            DependencyProperty.Register(
                nameof(IconGlyph),
                typeof(string),
                typeof(EmptyState),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(
                nameof(Message),
                typeof(string),
                typeof(EmptyState),
                new PropertyMetadata(string.Empty));

        public string IconGlyph
        {
            get => (string)GetValue(IconGlyphProperty);
            set => SetValue(IconGlyphProperty, value);
        }

        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }
    }
}
