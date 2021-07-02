using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace WinPuzzle
{
   #region AnimationFrameEventArgs

   /// <summary>
   /// 
   /// </summary>
   public class AnimationFrameEventArgs : EventArgs
   {
      /// <summary>  </summary>
      public readonly float Elapsed;

      /// <summary>  </summary>
      public AnimationFrameEventArgs( float elapsed )
      {
         this.Elapsed = elapsed;
      }
   }

   #endregion AnimationFrameEventArgs

   /// <summary>
   /// 
   /// </summary>
   public class ControlAnimationHandler
   {
      #region Fields and Properties

      Timer timer;
      long lastTick;

      float duration;
      /// <summary> Gets or sets the duration for the animation in seconds. Set to negative value to run continuously. </summary>
      public float Duration
      {
         get { return this.duration; }
         set { this.duration = value; }
      }

      float elapsedTime;
      /// <summary> Gets the total elapsed time the animation has been running. </summary>
      public float ElapsedTime
      {
         get { return this.elapsedTime; }
      }

      /// <summary> Gets or sets whether the animation runs continuously. </summary>
      public bool IsContinuous
      {
         get { return this.duration < 0f; }
         set { this.duration = value ? -1f : 0f; }
      }

      /// <summary>  </summary>
      public bool IsRunning
      {
         get { return this.timer.Enabled; }
         set
         {
            if( this.IsRunning != value )
            {
               if( value )
                  Start();
               else
                  Stop();
            }
         }
      }

      object stateData;
      /// <summary> Gets or sets optional animation state data for this ControlAnimationHandler. </summary>
      public object StateData
      {
         get { return this.stateData; }
         set { this.stateData = value; }
      }

      #endregion Fields and Properties

      #region Construction

      /// <summary>  </summary>
      public ControlAnimationHandler()
      {
         // set default continuous run time
         this.duration = -1f;
         // create default timer, with 30 ms frame-rate
         this.timer = new Timer();
         this.timer.Interval = 30;
         // hook-up timer tick callback
         this.timer.Tick += OnTick;
      }

      #endregion Construction

      #region Events

      /// <summary>  </summary>
      public event EventHandler AnimationStarted;

      /// <summary>  </summary>
      public event EventHandler AnimationStopped;

      /// <summary>  </summary>
      public event EventHandler<AnimationFrameEventArgs> AnimationFrame;

      #endregion Events

      #region Animation Behavior

      void OnTick( object sender, EventArgs e )
      {
         // calculate elapsed time since last frame
         long currTick = Stopwatch.GetTimestamp();
         long elapsedTick = currTick - this.lastTick;

         // update the last tick
         this.lastTick = currTick;

         // calculate elapsed time in seconds
         float elapsedFrameTime = (float)( (double)elapsedTick / Stopwatch.Frequency );

         // add to total elapsed time
         this.elapsedTime += elapsedFrameTime;

         // create event args for frame animate event
         AnimationFrameEventArgs frameEventArgs = new AnimationFrameEventArgs( elapsedFrameTime );
         // notify of animation frame tick event
         this.AnimationFrame?.Invoke( this, frameEventArgs );

         // check for animation time-out, comparing to the set duration
         if( !this.IsContinuous )
         {
            float remainingTime = this.duration - this.elapsedTime;
            if( remainingTime <= 0f )
            {
               // stop animation timer
               Stop();
            }
         }
      }

      /// <summary>  </summary>
      public void Start()
      {
         if( this.IsRunning )
            return;

         // notify of animation started event
         this.AnimationStarted?.Invoke( this, EventArgs.Empty );

         // set initial start time stamp
         this.lastTick = Stopwatch.GetTimestamp();

         // reset elapsed time
         this.elapsedTime = 0f;

         // begin animation timer
         this.timer.Start();
      }

      /// <summary>  </summary>
      public void Stop()
      {
         if( !this.IsRunning )
            return;

         // stop animation timer
         this.timer.Stop();

         // notify of animation stopped event
         this.AnimationStopped?.Invoke( this, EventArgs.Empty );
      }

      #endregion Animation Behavior
   }

}