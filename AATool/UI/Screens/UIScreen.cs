﻿using System;
using System.Windows.Forms;
using System.Xml;
using AATool.Configuration;
using AATool.Graphics;
using AATool.UI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AATool.UI.Screens
{
    public abstract class UIScreen : UIControl
    {
        public SwapChainRenderTarget SwapChain { get; protected set; }
        public Form Form                       { get; private set; }
        public Main Main                       { get; private set; }
        public GameWindow Window               { get; private set; }
        public GraphicsDevice GraphicsDevice   { get; private set; }

        public int FormWidth  => this.Form.ClientRectangle.Width;
        public int FormHeight => this.Form.ClientRectangle.Height;
        public bool HasFocus  => this.Form.Focused;

        public abstract Color FrameBackColor();
        public abstract Color FrameBorderColor();

        public UIScreen(Main main, GameWindow window)
        {
            this.Main           = main;
            this.Window         = window;
            this.GraphicsDevice = main.GraphicsDevice;
            this.DrawMode       = DrawMode.All;
            this.Form           = Control.FromHandle(window.Handle) as Form;
            this.Form.Icon      = new System.Drawing.Icon(Paths.System.MainIcon);
            this.Form.ShowIcon  = true;
        }

        public void Show() => this.Form.Show();
        public void Hide() => this.Form.Hide();

        public virtual void Dispose()
        {
            this.SwapChain?.Dispose();
            this.Form.Dispose();
        }

        public virtual void Prepare(Canvas canvas) =>  
            this.GraphicsDevice.SetRenderTarget(this.SwapChain);

        public virtual void Present(Canvas canvas) => 
            this.SwapChain?.Present();

        public override void MoveTo(Point point) =>
            this.Form.Location = new System.Drawing.Point(point.X, point.Y);

        public override void MoveBy(Point point) =>
            this.Form.Location = new System.Drawing.Point(this.Form.Location.X + point.X, this.Form.Location.Y + point.Y);

        public override void ScaleTo(Point point)
        {
            this.Form.ClientSize = new System.Drawing.Size(point.X, point.Y);
        }

        public override void ResizeThis(Rectangle parent)
        {
            this.Bounds  = new Rectangle(this.Bounds.Location, parent.Size);
            this.Inner = new Rectangle(Point.Zero, parent.Size);
        }

        public override void DrawRecursive(Canvas canvas)
        {
            canvas.BeginDraw(this);
            base.DrawRecursive(canvas);
            if (Config.Main.LayoutDebugMode)
                this.DrawDebugRecursive(canvas);
            canvas.EndDraw(this);
        }

        public override void DrawDebugRecursive(Canvas canvas)
        {
            for (int i = 0; i < this.Children.Count; i++)
                this.Children[i].DrawDebugRecursive(canvas);
        }

        public abstract void ReloadLayout();
        protected abstract void ConstrainWindow();
    }
}
