﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms.VisualStyles;

namespace System.Windows.Forms;

public abstract partial class UpDownBase
{
    /// <summary>
    ///  A control representing the pair of buttons on the end of the up-down edit control. This class handles
    ///  drawing the up-down buttons, and detecting mouse actions on these buttons. Acceleration on the buttons is
    ///  handled. The control sends UpDownEventArgs to the parent UpDownBase class when a button is pressed, or
    ///  when the acceleration determines that another event should be generated.
    /// </summary>
    internal partial class UpDownButtons : Control
    {
        private readonly UpDownBase _parent;

        // Button state
        private ButtonID _pushed = ButtonID.None;
        private ButtonID _captured = ButtonID.None;
        private ButtonID _mouseOver = ButtonID.None;

        private UpDownEventHandler? _upDownEventHandler;

        private Timer? _timer; // generates UpDown events
        private int _timerInterval; // milliseconds between events

        private Bitmap? _cachedBitmap;

        private bool _doubleClickFired;

        /// <summary>
        ///  Initializes a new instance of the <see cref="UpDownButtons"/> class.
        /// </summary>
        /// <param name="parent">The parent <see cref="UpDownBase"/> control.</param>
        internal UpDownButtons(UpDownBase parent) : base()
        {
            SetStyle(ControlStyles.Opaque | ControlStyles.FixedHeight | ControlStyles.FixedWidth, true);
            SetStyle(ControlStyles.Selectable, false);
            _parent = parent;
        }

        /// <summary>
        ///  Adds a handler for the UpDown button event.
        /// </summary>
        public event UpDownEventHandler? UpDown
        {
            add => _upDownEventHandler += value;
            remove => _upDownEventHandler -= value;
        }

        /// <summary>
        ///  Called when the mouse button is pressed - we need to start spinning the value of the up-down control.
        /// </summary>
        /// <param name="e">The mouse event arguments.</param>
        private void BeginButtonPress(MouseEventArgs e)
        {
            int half_height = Size.Height / 2;

            if (e.Y < half_height)
            {
                // Up button
                _pushed = _captured = ButtonID.Up;
                Invalidate();
            }
            else
            {
                // Down button
                _pushed = _captured = ButtonID.Down;
                Invalidate();
            }

            // Capture the mouse
            Capture = true;

            // Generate UpDown event
            OnUpDown(new UpDownEventArgs((int)_pushed));

            // Start the timer for new up-down events
            StartTimer();
        }

        /// <inheritdoc/>
        protected override AccessibleObject CreateAccessibilityInstance()
            => new UpDownButtonsAccessibleObject(this);

        /// <summary>
        ///  Called when the mouse button is released - we need to stop spinning the value of the up-down control.
        /// </summary>
        private void EndButtonPress()
        {
            _pushed = ButtonID.None;
            _captured = ButtonID.None;

            // Stop the timer
            StopTimer();

            // Release the mouse
            Capture = false;

            // Redraw the buttons
            Invalidate();
        }

        /// <summary>
        ///  Handles detecting mouse hits on the buttons. This method detects
        ///  which button was hit (up or down), fires an up-down event, captures
        ///  the mouse, and starts a timer for repeated up-down events.
        /// </summary>
        /// <param name="e">The mouse event arguments.</param>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            // Begin spinning the value
            // Focus the parent
            _parent.Focus();

            if (!_parent.ValidationCancelled && e.Button == MouseButtons.Left)
            {
                BeginButtonPress(e);
            }

            if (e.Clicks == 2 && e.Button == MouseButtons.Left)
            {
                _doubleClickFired = true;
            }

            // At no stage should a button be pushed, and the mouse
            // not captured.
            Debug.Assert(
                !(_pushed != ButtonID.None && _captured == ButtonID.None),
                "Invalid button pushed/captured combination");

            _parent.OnMouseDown(_parent.TranslateMouseEvent(this, e));
        }

        /// <inheritdoc/>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            // If the mouse is captured by the buttons (i.e. an up-down button
            // was pushed, and the mouse button has not yet been released),
            // determine the new state of the buttons depending on where
            // the mouse pointer has moved.

            if (Capture)
            {
                // Determine button area
                Rectangle rect = ClientRectangle;
                rect.Height /= 2;

                if (_captured == ButtonID.Down)
                {
                    rect.Y += rect.Height;
                }

                // Test if the mouse has moved outside the button area
                if (rect.Contains(e.X, e.Y))
                {
                    // Inside button, re-push the button if necessary
                    if (_pushed != _captured)
                    {
                        // Restart the timer
                        StartTimer();

                        _pushed = _captured;
                        Invalidate();
                    }
                }
                else
                {
                    // Outside button
                    //
                    // Retain the capture, but pop the button up while the mouse
                    // remains outside the button and the mouse button remains pressed.
                    if (_pushed != ButtonID.None)
                    {
                        // Stop the timer for up-down events
                        StopTimer();

                        _pushed = ButtonID.None;
                        Invalidate();
                    }
                }
            }

            // Logic for seeing which button is Hot if any
            Rectangle rectUp = ClientRectangle, rectDown = ClientRectangle;
            rectUp.Height /= 2;
            rectDown.Y += rectDown.Height / 2;

            // Check if the mouse is on the upper or lower button. Note that it could be in neither.
            if (rectUp.Contains(e.X, e.Y))
            {
                _mouseOver = ButtonID.Up;
                Invalidate();
            }
            else if (rectDown.Contains(e.X, e.Y))
            {
                _mouseOver = ButtonID.Down;
                Invalidate();
            }

            // At no stage should a button be pushed, and the mouse not captured.
            Debug.Assert(
                !(_pushed != ButtonID.None && _captured == ButtonID.None),
                "Invalid button pushed/captured combination");

            _parent.OnMouseMove(_parent.TranslateMouseEvent(this, e));
        }

        /// <summary>
        ///  Handles detecting when the mouse button is released.
        /// </summary>
        /// <param name="e">The mouse event arguments.</param>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (!_parent.ValidationCancelled && e.Button == MouseButtons.Left)
            {
                EndButtonPress();
            }

            // At no stage should a button be pushed, and the mouse not captured.
            Debug.Assert(
                !(_pushed != ButtonID.None && _captured == ButtonID.None),
                "Invalid button pushed/captured combination");

            MouseEventArgs me = _parent.TranslateMouseEvent(this, e);
            if (e.Button == MouseButtons.Left)
            {
                if (!_parent.ValidationCancelled && PInvoke.WindowFromPoint(PointToScreen(e.Location)) == HWND)
                {
                    if (!_doubleClickFired)
                    {
                        _parent.OnClick(me);
                    }
                    else
                    {
                        _doubleClickFired = false;
                        _parent.OnDoubleClick(me);
                        _parent.OnMouseDoubleClick(me);
                    }
                }

                _doubleClickFired = false;
            }

            _parent.OnMouseUp(me);
        }

        /// <inheritdoc/>
        protected override void OnMouseLeave(EventArgs e)
        {
            _mouseOver = ButtonID.None;
            Invalidate();

            _parent.OnMouseLeave(e);
        }

        /// <summary>
        ///  Handles painting the buttons on the control.
        /// </summary>
        /// <param name="e">The paint event arguments.</param>
#pragma warning disable WFO5001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        protected override void OnPaint(PaintEventArgs e)
        {
            int half_height = ClientSize.Height / 2;

            // Draw the up and down buttons
            if (Application.IsDarkModeEnabled || !Application.RenderWithVisualStyles)
            {
                Graphics cachedGraphics = EnsureCachedBitmap(
                    _parent._defaultButtonsWidth,
                    ClientSize.Height);

                ControlPaint.DrawScrollButton(
                    cachedGraphics,
                    new Rectangle(0, 0, _parent._defaultButtonsWidth, half_height),
                    ScrollButton.Up,
                    _pushed == ButtonID.Up
                        ? ButtonState.Pushed
                        : (Enabled ? ButtonState.Normal : ButtonState.Inactive));

                ControlPaint.DrawScrollButton(
                    cachedGraphics,
                    new Rectangle(0, half_height, _parent._defaultButtonsWidth, half_height),
                    ScrollButton.Down,
                    _pushed == ButtonID.Down
                        ? ButtonState.Pushed
                        : (Enabled ? ButtonState.Normal : ButtonState.Inactive));

                e.GraphicsInternal.DrawImageUnscaled(
                    _cachedBitmap,
                    new Point(0, 0));
            }
            else
            {
                VisualStyleRenderer vsr = new(_mouseOver == ButtonID.Up
                    ? VisualStyleElement.Spin.Up.Hot
                    : VisualStyleElement.Spin.Up.Normal);

                if (!Enabled)
                {
                    vsr.SetParameters(VisualStyleElement.Spin.Up.Disabled);
                }
                else if (_pushed == ButtonID.Up)
                {
                    vsr.SetParameters(VisualStyleElement.Spin.Up.Pressed);
                }

                using DeviceContextHdcScope hdc = new(e);
                vsr.DrawBackground(
                    hdc,
                    new Rectangle(0, 0, _parent._defaultButtonsWidth, half_height),
                    HWNDInternal);

                if (!Enabled)
                {
                    vsr.SetParameters(VisualStyleElement.Spin.Down.Disabled);
                }
                else if (_pushed == ButtonID.Down)
                {
                    vsr.SetParameters(VisualStyleElement.Spin.Down.Pressed);
                }
                else
                {
                    vsr.SetParameters(_mouseOver == ButtonID.Down
                        ? VisualStyleElement.Spin.Down.Hot
                        : VisualStyleElement.Spin.Down.Normal);
                }

                vsr.DrawBackground(
                    hdc,
                    new Rectangle(0, half_height, _parent._defaultButtonsWidth, half_height),
                    HWNDInternal);
            }
#pragma warning restore WFO5001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

            if (half_height != (ClientSize.Height + 1) / 2)
            {
                // When control has odd height, a line needs to be drawn below the buttons with the BackColor.
                Color color = _parent.BackColor;

                Rectangle clientRect = ClientRectangle;
                Point pt1 = new(clientRect.Left, clientRect.Bottom - 1);
                Point pt2 = new(clientRect.Right, clientRect.Bottom - 1);

                using DeviceContextHdcScope hdc = new(e);
                using CreatePenScope hpen = new(color);
                hdc.DrawLine(hpen, pt1, pt2);
            }

            // Raise the paint event, just in case this inner class goes public some day
            base.OnPaint(e);
        }

        /// <summary>
        ///  Ensures that the Bitmap has the correct size and returns the Graphics object from that Bitmap.
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        [MemberNotNull(nameof(_cachedBitmap))]
        private Graphics EnsureCachedBitmap(int width, int height)
        {
            if (_cachedBitmap is null || _cachedBitmap.Size != new Size(width, height))
            {
                _cachedBitmap?.Dispose();
                _cachedBitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            }

            return Graphics.FromImage(_cachedBitmap);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _cachedBitmap?.Dispose();
                _cachedBitmap = null;
                _timer?.Dispose();
                _timer = null;
                _upDownEventHandler = null;
            }
        }

        /// <summary>
        ///  Occurs when the UpDown buttons are pressed and when the acceleration timer tick event is raised.
        /// </summary>
        /// <param name="upevent">The up-down event arguments.</param>
        protected virtual void OnUpDown(UpDownEventArgs upevent)
            => _upDownEventHandler?.Invoke(this, upevent);

        /// <inheritdoc/>
        internal override void ReleaseUiaProvider(HWND handle)
        {
            if (IsAccessibilityObjectCreated
                && OsVersion.IsWindows8OrGreater()
                && AccessibilityObject is UpDownButtonsAccessibleObject buttonsAccessibilityObject)
            {
                buttonsAccessibilityObject.ReleaseChildUiaProviders();
            }

            base.ReleaseUiaProvider(handle);
        }

        /// <summary>
        ///  Starts the timer for generating up-down events.
        /// </summary>
        protected void StartTimer()
        {
            _parent.OnStartTimer();
            if (_timer is null)
            {
                // Generates UpDown events
                _timer = new Timer();

                // Add the timer handler
                _timer.Tick += TimerHandler;
            }

            _timerInterval = DefaultTimerInterval;

            _timer.Interval = _timerInterval;
            _timer.Start();
        }

        /// <summary>
        ///  Stops the timer for generating up-down events.
        /// </summary>
        protected void StopTimer()
        {
            if (_timer is not null)
            {
                _timer.Stop();
                _timer.Dispose();
                _timer = null;
            }

            _parent.OnStopTimer();
        }

        /// <inheritdoc/>
        internal override bool SupportsUiaProviders => true;

        /// <summary>
        ///  Generates up-down events when the timer calls this function.
        /// </summary>
        /// <param name="source">The source of the event.</param>
        /// <param name="args">The event arguments.</param>
        private void TimerHandler(object? source, EventArgs args)
        {
            // Make sure we've got mouse capture
            if (!Capture)
            {
                EndButtonPress();
                return;
            }

            // OnUpDown method calls customer's ValueChanged event handler which might enter the message loop and
            // process the mouse button up event, which results in timer being disposed
            OnUpDown(new UpDownEventArgs((int)_pushed));

            if (_timer is not null)
            {
                // Accelerate timer.
                _timerInterval *= 7;
                _timerInterval /= 10;

                if (_timerInterval < 1)
                {
                    _timerInterval = 1;
                }

                _timer.Interval = _timerInterval;
            }
        }
    }
}
