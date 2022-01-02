﻿using System.Windows;
using System.Windows.Controls;
using PicView.Animations;
using PicView.Properties;
using PicView.UILogic.Sizing;
using static PicView.Animations.MouseOverAnimations;

namespace PicView.Views.UserControls
{
    public partial class FullscreenButton : UserControl
    {
        public FullscreenButton()
        {
            InitializeComponent();

            MouseEnter += delegate
            {
                if (Settings.Default.Fullscreen)
                {
                    ToolTip = Application.Current.Resources["RestoreDown"];
                }
                else
                {
                    ToolTip = Application.Current.Resources["Fullscreen"];
                }

                ButtonMouseOverAnim(FullscreenButtonBrush, true);
            };

            MouseLeave += delegate
            {
                ButtonMouseLeaveAnim(FullscreenButtonBrush, true);
            };

            TheButton.Click += delegate
            {
                WindowSizing.Fullscreen_Restore();
            };

            if (!Settings.Default.DarkTheme)
            {
                AnimationHelper.LightThemeMouseEvent(this, IconBrush);
            }
        }
    }
}