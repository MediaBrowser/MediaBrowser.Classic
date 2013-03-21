using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Configurator.Code
{
    public class PopupMsg
    {
        private TextBlock msg;

        public PopupMsg(TextBlock msg)
        {
            //Store TextBlock for message display
            this.msg = msg;
            //Register the textblock's name, this is necessary for creating Storyboard using codes instead of XAML
            NameScope.SetNameScope(msg, new NameScope());
            msg.RegisterName("fadetext", msg);

            //Create the fade in & fade out animation
            DoubleAnimationUsingKeyFrames fadeInOutAni = new DoubleAnimationUsingKeyFrames();
            LinearDoubleKeyFrame keyframe = new LinearDoubleKeyFrame();
            keyframe.KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2));
            keyframe.Value = 1;
            fadeInOutAni.KeyFrames.Add(keyframe);
            //keyframe = new LinearDoubleKeyFrame();

            fadeInOutAni.Duration = new Duration(TimeSpan.FromSeconds(4));        
            fadeInOutAni.AutoReverse = true;
            fadeInOutAni.AccelerationRatio = .2;
            fadeInOutAni.DecelerationRatio = .7;

            // Configure the animation to target the message's opacity property
            Storyboard.SetTargetName(fadeInOutAni, "fadetext");
            Storyboard.SetTargetProperty(fadeInOutAni, new PropertyPath(TextBlock.OpacityProperty));

            // Add the fade in & fade out animation to the Storyboard
            Storyboard fadeInOutStoryBoard = new Storyboard();
            fadeInOutStoryBoard.Children.Add(fadeInOutAni);

            // Set event trigger, make this animation played on an event we can control
            msg.IsVisibleChanged += delegate(object sender,  System.Windows.DependencyPropertyChangedEventArgs e)
            {
                if (msg.IsVisible) fadeInOutStoryBoard.Begin(msg);
            };
        }

        public void DisplayMessage(string message)
        {
            msg.Opacity = 0; //start invisible
            msg.Text = message;
            //fire our event
            msg.Visibility = Visibility.Hidden;
            msg.Visibility = Visibility.Visible;
        }

        
        
    }
}
