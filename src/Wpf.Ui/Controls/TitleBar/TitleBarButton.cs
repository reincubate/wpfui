// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;
using Windows.Win32;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

public class TitleBarButton : Wpf.Ui.Controls.Button
{
    /// <summary>Identifies the <see cref="ButtonType"/> dependency property.</summary>
    public static readonly DependencyProperty ButtonTypeProperty = DependencyProperty.Register(
        nameof(ButtonType),
        typeof(TitleBarButtonType),
        typeof(TitleBarButton),
        new PropertyMetadata(TitleBarButtonType.Unknown, OnButtonTypeChanged)
    );

    /// <summary>Identifies the <see cref="ButtonsForeground"/> dependency property.</summary>
    public static readonly DependencyProperty ButtonsForegroundProperty = DependencyProperty.Register(
        nameof(ButtonsForeground),
        typeof(Brush),
        typeof(TitleBarButton),
        new FrameworkPropertyMetadata(
            SystemColors.ControlTextBrush,
            FrameworkPropertyMetadataOptions.Inherits
        )
    );

    /// <summary>Identifies the <see cref="MouseOverButtonsForeground"/> dependency property.</summary>
    public static readonly DependencyProperty MouseOverButtonsForegroundProperty =
        DependencyProperty.Register(
            nameof(MouseOverButtonsForeground),
            typeof(Brush),
            typeof(TitleBarButton),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits)
        );

    /// <summary>Identifies the <see cref="RenderButtonsForeground"/> dependency property.</summary>
    public static readonly DependencyProperty RenderButtonsForegroundProperty = DependencyProperty.Register(
        nameof(RenderButtonsForeground),
        typeof(Brush),
        typeof(TitleBarButton),
        new FrameworkPropertyMetadata(
            SystemColors.ControlTextBrush,
            FrameworkPropertyMetadataOptions.Inherits
        )
    );

    /// <summary>
    /// Gets or sets the type of the button.
    /// </summary>
    public TitleBarButtonType ButtonType
    {
        get => (TitleBarButtonType)GetValue(ButtonTypeProperty);
        set => SetValue(ButtonTypeProperty, value);
    }

    /// <summary>
    /// Gets or sets the foreground of the navigation buttons.
    /// </summary>
    public Brush ButtonsForeground
    {
        get => (Brush)GetValue(ButtonsForegroundProperty);
        set => SetValue(ButtonsForegroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the foreground of the navigation buttons when moused over.
    /// </summary>
    public Brush? MouseOverButtonsForeground
    {
        get => (Brush?)GetValue(MouseOverButtonsForegroundProperty);
        set => SetValue(MouseOverButtonsForegroundProperty, value);
    }

    public Brush RenderButtonsForeground
    {
        get => (Brush)GetValue(RenderButtonsForegroundProperty);
        set => SetValue(RenderButtonsForegroundProperty, value);
    }

    public bool IsHovered { get; private set; }

    private readonly Brush _defaultBackgroundBrush = Brushes.Transparent; // REVIEW: Should it be transparent?
    private uint _returnValue;

    private bool _isClickedDown;

    // True while a WPF (WISP) touch is down on this button. OnTouchDown fires before the touch-promoted
    // non-client WM_NCLBUTTONUP reaches ReactToHwndHook and OnTouchUp fires after it, so this is reliably set
    // exactly when the hook processes a touch tap - letting it tell a touch (which WPF also turns into a click)
    // apart from a genuine mouse click. Only ever touched on the UI thread (WPF touch events and the hwnd hook
    // both run there), so it needs no synchronization.
    private bool _touchInProgress;

    // True while a WPF (WISP) pen/stylus contact is down on this button. A pen tap does NOT raise WPF Touch
    // events (it comes through the Stylus stack), so _touchInProgress never catches it and the NC hook fires a
    // duplicate click - the same double-toggle as sc-54268, but for the pen (sc-56373). Stylus events also fire
    // for finger touch, which is harmless: the finger case stays guarded by _touchInProgress. UI thread only.
    private bool _stylusInProgress;

    public TitleBarButton()
    {
        Loaded += TitleBarButton_Loaded;
        Unloaded += TitleBarButton_Unloaded;
    }

    private void TitleBarButton_Unloaded(object sender, RoutedEventArgs e)
    {
        DependencyPropertyDescriptor
            .FromProperty(ButtonsForegroundProperty, typeof(Brush))
            .RemoveValueChanged(this, OnButtonsForegroundChanged);
    }

    private void TitleBarButton_Loaded(object sender, RoutedEventArgs e)
    {
        SetCurrentValue(RenderButtonsForegroundProperty, ButtonsForeground);
        DependencyPropertyDescriptor
            .FromProperty(ButtonsForegroundProperty, typeof(Brush))
            .AddValueChanged(this, OnButtonsForegroundChanged);
    }

    private void OnButtonsForegroundChanged(object? sender, EventArgs e)
    {
        SetCurrentValue(
            RenderButtonsForegroundProperty,
            IsHovered ? MouseOverButtonsForeground : ButtonsForeground
        );
    }

    /// <summary>
    /// Forces button background to change.
    /// </summary>
    public void Hover()
    {
        if (IsHovered)
        {
            return;
        }

        SetCurrentValue(BackgroundProperty, MouseOverBackground);
        if (MouseOverButtonsForeground != null)
        {
            SetCurrentValue(RenderButtonsForegroundProperty, MouseOverButtonsForeground);
        }

        IsHovered = true;
    }

    /// <summary>
    /// Forces button background to change.
    /// </summary>
    public void RemoveHover()
    {
        if (!IsHovered)
        {
            return;
        }

        SetCurrentValue(BackgroundProperty, _defaultBackgroundBrush);
        SetCurrentValue(RenderButtonsForegroundProperty, ButtonsForeground);

        IsHovered = false;
        _isClickedDown = false;
    }

    /// <summary>
    /// Invokes click on the button.
    /// </summary>
    public void InvokeClick()
    {
        if (
            new ButtonAutomationPeer(this).GetPattern(PatternInterface.Invoke)
            is IInvokeProvider invokeProvider
        )
        {
            invokeProvider.Invoke();
        }

        _isClickedDown = false;
    }

    // See _touchInProgress: track whether a WISP touch is currently down on this button so ReactToHwndHook can
    // suppress its synthetic click for touch (WPF delivers that click itself, so the hook's would be a duplicate).
    protected override void OnTouchDown(TouchEventArgs e)
    {
        _touchInProgress = true;
        base.OnTouchDown(e);
    }

    protected override void OnTouchUp(TouchEventArgs e)
    {
        _touchInProgress = false;
        base.OnTouchUp(e);
    }

    // Also clear if the touch never delivers an Up (finger dragged off the button, capture lost) so a later
    // mouse click on this button is not mistaken for a touch.
    protected override void OnTouchLeave(TouchEventArgs e)
    {
        _touchInProgress = false;
        base.OnTouchLeave(e);
    }

    protected override void OnLostTouchCapture(TouchEventArgs e)
    {
        _touchInProgress = false;
        base.OnLostTouchCapture(e);
    }

    // See _stylusInProgress: mirror the touch tracking for pen/stylus, which WPF Touch events don't cover.
    protected override void OnStylusDown(StylusDownEventArgs e)
    {
        _stylusInProgress = true;
        base.OnStylusDown(e);
    }

    protected override void OnStylusUp(StylusEventArgs e)
    {
        _stylusInProgress = false;
        base.OnStylusUp(e);
    }

    // Also clear if the stylus never delivers an Up (dragged off the button, capture lost) so a later mouse
    // click on this button is not mistaken for a pen tap.
    protected override void OnStylusLeave(StylusEventArgs e)
    {
        _stylusInProgress = false;
        base.OnStylusLeave(e);
    }

    protected override void OnLostStylusCapture(StylusEventArgs e)
    {
        _stylusInProgress = false;
        base.OnLostStylusCapture(e);
    }

    internal bool ReactToHwndHook(uint msg, IntPtr lParam, out IntPtr returnIntPtr)
    {
        returnIntPtr = IntPtr.Zero;

        switch (msg)
        {
            case PInvoke.WM_NCHITTEST:
                if (this.IsMouseOverElement(lParam))
                {
                    /*Debug.WriteLine($"Hitting {ButtonType} | return code {_returnValue}");*/
                    Hover();
                    returnIntPtr = (IntPtr)_returnValue;
                    return true;
                }

                RemoveHover();
                return false;
            case PInvoke.WM_NCMOUSELEAVE: // Mouse leaves the window
                RemoveHover();
                return false;
            case PInvoke.WM_NCLBUTTONDOWN when this.IsMouseOverElement(lParam): // Left button clicked down
                _isClickedDown = true;
                return true;
            case PInvoke.WM_NCLBUTTONUP when _isClickedDown && this.IsMouseOverElement(lParam): // Left button clicked up
                _isClickedDown = false;

                // A touch or pen tap is ALSO delivered by WPF's WISP stack as a promoted mouse click. Calling
                // InvokeClick would then result in a doubled activation, e.g. maximizing & instantly restoring the
                // window. Touch is caught by _touchInProgress; pen (which raises no Touch events) by _stylusInProgress.
                if (!_touchInProgress && !_stylusInProgress)
                {
                    InvokeClick();
                }

                return true;
            default:
                return false;
        }
    }

    private static void OnButtonTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TitleBarButton titleBarButton)
        {
            return;
        }

        titleBarButton.OnButtonTypeChanged(e);
    }

    protected void OnButtonTypeChanged(DependencyPropertyChangedEventArgs e)
    {
        var buttonType = (TitleBarButtonType)e.NewValue;

        _returnValue = buttonType switch
        {
            TitleBarButtonType.Unknown => PInvoke.HTNOWHERE,
            TitleBarButtonType.Help => PInvoke.HTHELP,
            TitleBarButtonType.Minimize => PInvoke.HTMINBUTTON,
            TitleBarButtonType.Close => PInvoke.HTCLOSE,
            TitleBarButtonType.Restore => PInvoke.HTMAXBUTTON,
            TitleBarButtonType.Maximize => PInvoke.HTMAXBUTTON,
            _ => throw new ArgumentOutOfRangeException(
                "e.NewValue",
                buttonType,
                $"Unsupported button type: {buttonType}."
            ),
        };
    }

    // TODO: Incorrectly calculates mouse position for high DPI displays.
    // PresentationSource presentationSource = null;
    // protected bool IsMouseOverElement(nint lParam)
    // {
    //    System.Drawing.Point winPoint;
    //    bool gotCursorPos = User32.GetCursorPos(out winPoint);

    //    if (!gotCursorPos)
    //    {
    //        int fallbackX = unchecked((short)((long)lParam & 0xFFFF));
    //        int fallbackY = unchecked((short)(((long)lParam >> 16) & 0xFFFF));
    //        winPoint = new System.Drawing.Point(fallbackX, fallbackY);
    //    }

    //    var screenPoint = new System.Windows.Point(winPoint.X, winPoint.Y);

    //    presentationSource ??= PresentationSource.FromVisual(this);

    //    if (presentationSource?.CompositionTarget != null)
    //    {
    //        screenPoint = presentationSource.CompositionTarget.TransformFromDevice.Transform(screenPoint);
    //    }

    //    var localPoint = this.PointFromScreen(screenPoint);

    //    var hitTestRect = new System.Windows.Rect(0, 0, this.ActualWidth, this.ActualHeight);

    //    return hitTestRect.Contains(localPoint);
    //}
}
