﻿using System.Windows.Controls;
using PicView.Animations;
using PicView.Properties;
using static PicView.Animations.MouseOverAnimations;

namespace PicView.Views.UserControls
{
    public partial class CopyButton : UserControl
    {
        public CopyButton()
        {
            InitializeComponent();

            Loaded += delegate
            {
                TheButton.MouseEnter += (s, x) => ButtonMouseOverAnim(CopyButtonBrush, true);
                TheButton.MouseLeave += (s, x) => ButtonMouseLeaveAnimBgColor(CopyButtonBrush);

                if (!Settings.Default.DarkTheme)
                {
                    AnimationHelper.LightThemeMouseEvent(this, IconBrush);
                }
            };
        }
    }
}