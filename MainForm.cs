using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;

namespace WinPuzzle
{
   /// <summary>
   /// MainForm for WinPuzzle game.
   /// </summary>
   public class MainForm : Form
   {
      int size = 4;
      bool numbers = true;
      int[,] tiles;
      Image image;
      bool solved = true;
      bool scrambled = false;
      int moves;
      int startTime;

      bool maintainAspect = true;

      string[] imageNames;
      int currImageIndex;

      string[] textLines;
      ControlAnimationHandler animationHandler;
      float animateRatio;

      enum AnimationState
      {
         Idle,
         Expand,
         Collapse,
      }

      AnimationState animateState = AnimationState.Idle;

      // TODO:
      // * context menu..? -- on-screen menu, hamburger or back-button..?
      // * level progression -- move to next after solve current
      // * save high scores - fastest solve times (for each puzzle image)
      // * enable image invert, for all images
      // * option to change cursor tile draw style (checker dark/light, solid color...)
      // * add some other new image logos (Edge, VS, Win10..?)

      ContextMenu contextMenu;

      public MainForm()
      {
         this.Text = "WinPuzzle";
         this.ClientSize = new Size( 500, 500 );
         this.FormBorderStyle = FormBorderStyle.Sizable;
         this.MinimumSize = new Size( 256, 256 );
         this.SetStyle( ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.DoubleBuffer, true );

         this.Icon = new Icon( this.GetType().Assembly.GetManifestResourceStream( "WinPuzzle.App.ico" ) );

         LoadImageResources( "WinPuzzle.Images" );
         SetImageIndex( "WindowsLogo" );

         InitializeContextMenu();

         CreatePuzzle( size );

         this.textLines = new string[]
            {
               "WinPuzzle",
               "v1.0",
               "Created by Taber",
               "with a computer",
            };

         this.animationHandler = new ControlAnimationHandler();
         this.animationHandler.Duration = .5f;
         this.animationHandler.AnimationFrame += AnimationHandler_AnimationFrame;
         this.animationHandler.AnimationStarted += AnimationHandler_AnimationStarted;
         this.animationHandler.AnimationStopped += AnimationHandler_AnimationStopped;
      }

      void AnimationHandler_AnimationFrame( object sender, AnimationFrameEventArgs e )
      {
         // set updated animation progress complete ratio
         this.animateRatio = this.animationHandler.ElapsedTime / this.animationHandler.Duration;
         // request window redraw
         this.Invalidate();
      }

      void AnimationHandler_AnimationStarted( object sender, EventArgs e )
      {
         this.animateState = this.textLines != null ? AnimationState.Collapse : AnimationState.Expand;
         this.animateRatio = 0f;
      }

      void AnimationHandler_AnimationStopped( object sender, EventArgs e )
      {
         // set text lines to menu, if just expanded
         if( this.animateState == AnimationState.Expand )
         {
            this.textLines = new string[]
               {
                  "WinPuzzle",
                  "v1.0",
                  "Created by Taber",
                  "with a computer",
               };
         }
         else
         {
            this.textLines = null;
            //this.Invalidate();
         }

         // reset animation state
         this.animateState = AnimationState.Idle;
         this.animateRatio = 0f;
      }

      /// <summary> Clean up any resources being used. </summary>
      protected override void Dispose( bool disposing )
      {
         if( disposing )
         {
            if( this.image != null )
            {
               this.image.Dispose();
               this.image = null;
            }
         }
         base.Dispose( disposing );
      }

      void LoadImageResources( string filterText )
      {
         List<string> imageNameList = new List<string>();
         string[] resNames = this.GetType().Assembly.GetManifestResourceNames();
         for( int i = 0; i < resNames.Length; ++i )
         {
            if( resNames[i].StartsWith( filterText ) )
               imageNameList.Add( resNames[i] );
         }

         // sort images by name -- number prefix
         imageNameList.Sort();

         this.imageNames = imageNameList.ToArray();
      }

      bool SetImageIndex( string imageName )
      {
         int currIndex = -1;

         for( int i = 0; i < this.imageNames.Length; ++i )
         {
            if( this.imageNames[i].Contains( imageName ) )
            {
               currIndex = i;
               break;
            }
         }
         this.currImageIndex = currIndex;

         return this.currImageIndex != -1;
      }

      private void CreatePuzzle( int size )
      {
         this.size = size;
         this.tiles = new int[size, size];
         for( int x = 0; x < size; ++x )
         {
            for( int y = 0; y < size; ++y )
               tiles[x, y] = y * size + x;
         }
         // set last value as -1 for cursor char
         tiles[size - 1, size - 1] = -1;

         if( File.Exists( "image.bmp" ) )
         {
            this.image = Image.FromFile( "image.bmp" );
            this.numbers = false;
            //this.ClientSize = this.image.Size;
         }
         else if( this.currImageIndex != -1 ) // load embedded image
         {
            string imageFileName = this.imageNames[this.currImageIndex];
            this.image = Image.FromStream( this.GetType().Assembly.GetManifestResourceStream( imageFileName ) );
            this.numbers = false;
         }

         Shuffle();

         // reset game states
         this.moves = 0;
         this.startTime = Environment.TickCount; ;
      }

      private void Shuffle()
      {
         // 25 - minimal
         // 50 - challenge
         // pretty good
         Shuffle( 50 );
         // this.size * this.size * this.size // pretty good coverage
      }

      private void Shuffle( int amount )
      {
         //Random rand = new Random( 0 ); // fixed seed
         Random rand = new Random(); // random seed

         int success = 0;
         while( success < amount )
         {
            int curPos = GetCursor();
            int curX = curPos % this.size;
            int curY = curPos / this.size;
            int shiftX, shiftY;
            // pick random shift orientation
            if( rand.Next() % 2 == 0 ) // even, horizontal
            {
               shiftX = curX;
               shiftY = rand.Next( this.size );
            }
            else // odd, vertical
            {
               shiftX = rand.Next( this.size );
               shiftY = curY;
            }
            if( ShiftTiles( shiftX, shiftY ) )
               ++success;
         }
         CheckSolved();
         if( this.solved ) // no way...
            Shuffle();

         // set state of being scrambled -- can then only be solved once, until scramlbed again
         this.scrambled = true;
      }

      Rectangle GetPuzzleRect()
      {
         Rectangle rect = this.ClientRectangle;

         if( this.maintainAspect )
         {
            float imageAspect = (float)this.image.Width / this.image.Height;
            float winAspect = (float)rect.Width / rect.Height;
            if( winAspect > imageAspect )
            {
               int adjWidth = (int)( rect.Height * imageAspect );
               rect.X = ( rect.Width - adjWidth ) / 2;
               rect.Width = adjWidth;
            }
            else if( imageAspect > winAspect )
            {
               int adjHeight = (int)( rect.Width / imageAspect );
               rect.Y = ( rect.Height - adjHeight ) / 2;
               rect.Height = adjHeight;
            }
         }
         
         return rect;
      }

      Rectangle GetTextRect( Rectangle puzzleRect )
      {
         float textRatio = 0.8f;
         int textWidth = (int)( puzzleRect.Width * textRatio );
         int textHeight = (int)( puzzleRect.Height * textRatio );
         int textX = puzzleRect.X + ( puzzleRect.Width - textWidth ) / 2;
         int textY = puzzleRect.Y + ( puzzleRect.Height - textHeight ) / 2;
         Rectangle textRect = new Rectangle( textX, textY, textWidth, textHeight );
         return textRect;
      }

      protected override void OnPaint( PaintEventArgs e )
      {
         Rectangle puzzRect = GetPuzzleRect();

         int width = puzzRect.Width;
         int height = puzzRect.Height;
         int startX = puzzRect.X;
         int startY = puzzRect.Y;

         Graphics g = e.Graphics;

         int tileX = width / this.size;
         int tileY = height / this.size;

         SolidBrush solidBrush = new SolidBrush( Color.Gray );
         LinearGradientBrush gradientBrush = new LinearGradientBrush( new Rectangle( 0, 0, tileX, tileY ), Color.Azure, Color.SteelBlue, 45 );
         Font font = new Font( "Arial", Math.Min( tileX, tileY ) / 2, FontStyle.Bold, GraphicsUnit.Pixel );

         // retain cursor rect, used if animating
         Rectangle cursorRect = Rectangle.Empty;

         // draw tiles
         Rectangle destRect;
         int tileNum;
         for( int x = 0; x < size; ++x )
         {
            for( int y = 0; y < size; ++y )
            {
               destRect = new Rectangle( startX + x * tileX, startY + y * tileY, tileX, tileY );
               tileNum = tiles[x, y];

               // check for cursor tile number
               if( tileNum == -1 )
               {
                  // save cursor rect, used if animating menu
                  cursorRect = destRect;

                  // set cursor value to final number/image index if puzzle is solved
                  if( this.solved )
                     tileNum = this.size * this.size - 1;
               }

               // draw current tile rect
               if( tileNum == -1 ) // cursor square
               {
                  //g.FillRectangle( solidBrush, destRect );
                  //g.FillRectangle( Brushes.DarkGray, destRect );
                  //g.FillRectangle( Brushes.DimGray, destRect );
                  g.FillRectangle( Brushes.DarkGray, destRect );

                  destRect.Inflate( -4, -4 );
                  //g.FillRectangle( Brushes.Gray, destRect );

                  DrawChecker( g, destRect );

                  //g.FillRectangle( gradientBrush, destRect );
                  //g.FillRectangle( SystemBrushes.ActiveCaption, destRect );
               }
               else if( this.numbers )
               {
                  gradientBrush.ResetTransform();
                  gradientBrush.TranslateTransform( destRect.X, destRect.Y );

                  g.FillRectangle( gradientBrush, destRect );
                  //g.DrawString( tiles[x,y].ToString(), this.Font, Brushes.Black, x * tileX, y * tileY );
                  g.DrawString( string.Format( "{0}", tileNum + 1 ), font, Brushes.Black, startX + x * tileX + tileX / 4, startY + y * tileY + tileY / 4 );
               }
               else // draw section from image
               {
                  int imgX = this.image.Width / this.size;
                  int imgY = this.image.Height / this.size;

                  Rectangle srcRect = new Rectangle( tileNum % this.size * imgX, tileNum / this.size * imgY, imgX, imgY );
                  g.DrawImage( this.image, destRect, srcRect, GraphicsUnit.Pixel );

               }
               g.DrawRectangle( Pens.Black, destRect );
            }
         }

         // draw interpolated menu/text rect
         if( this.animateState != AnimationState.Idle )
         {
            Rectangle textRect = GetTextRect( puzzRect );
            float t = this.animateState == AnimationState.Expand ? this.animateRatio : 1f - this.animateRatio;
            Rectangle animRect = InterpolateRect( cursorRect, textRect, t );
            DrawTextLines( g, null, animRect );
         }
         // draw text lines
         else if( this.textLines != null )
         {
            //float textRatio = 0.8f;
            //int textWidth = (int)( width * textRatio );
            //int textHeight = (int)( height * textRatio );
            //int textX = startX + ( width - textWidth ) / 2;
            //int textY = startY + ( height - textHeight ) / 2;
            //Rectangle textRect = new Rectangle( textX, textY, textWidth, textHeight );
            Rectangle textRect = GetTextRect( puzzRect );
            DrawTextLines( g, this.textLines, textRect );   
         }

         solidBrush.Dispose();
         gradientBrush.Dispose();
         font.Dispose();
      }

      void DrawChecker( Graphics grfx, Rectangle rect )
      {
         int count = 8;
         Color[] colors = { Color.Gray, Color.DimGray };
         int c;
         SolidBrush brush = new SolidBrush( Color.White );
         float sx = rect.Width / (float)count;
         float sy = rect.Height / (float)count;
         for( int x = 0; x < count; ++x )
         {
            c = x % 2;
            for( int y = 0; y < count; ++y )
            {
               brush.Color = colors[c];
               grfx.FillRectangle( brush, rect.X + sx * x, rect.Y + sy * y, sx, sy );
               c = ( c + 1 ) % 2;
            }
         }
         brush.Dispose();
      }

      Rectangle InterpolateRect( Rectangle srcRect, Rectangle destRect, float t )
      {
         Rectangle intRect = Rectangle.Empty;

         intRect.X = srcRect.X + (int)( ( destRect.X - srcRect.X ) * t );
         intRect.Y = srcRect.Y + (int)( ( destRect.Y - srcRect.Y ) * t );
         intRect.Width = srcRect.Width + (int)( ( destRect.Width - srcRect.Width ) * t );
         intRect.Height = srcRect.Height + (int)( ( destRect.Height - srcRect.Height ) * t );

         return intRect;
      }

      bool CheckInputTextPopUp()
      {
         bool inputHandled = false;

         if( this.animationHandler.IsRunning 
            || this.animateState != AnimationState.Idle )
         {
            inputHandled = true;
         }
         else if( this.textLines != null )
         {
            // hide menu
            this.animationHandler.Start();

            //this.textLines = null;
            inputHandled = true;
            //this.Invalidate();
         }

         return inputHandled;
      }

      void DrawTextLines( Graphics grfx, string[] textLines, Rectangle textRect )
      {
         // fill background area for text display
         SolidBrush brush = new SolidBrush( Color.FromArgb( 160, Color.White ) );

         grfx.FillRectangle( brush, textRect );

         if( textLines != null )
         {
            brush.Color = Color.Black;

            int lineCount = textLines.Length;
            float lineHeight = textRect.Height / lineCount;
            float lineCenter = lineHeight / 2f;
            float fontHeight = lineHeight / 4f;

            Font font = new Font( "Arial", fontHeight, FontStyle.Regular, GraphicsUnit.Pixel );
            Font fontTitle = new Font( "Arial", fontHeight * 1.5f, FontStyle.Bold, GraphicsUnit.Pixel );

            float xLocCenter = textRect.X + textRect.Width / 2f;
            float yLocCenter;
            Font currFont = font;

            // draw lines
            for( int i = 0; i < textLines.Length; ++i )
            {
               currFont = i == 0 ? fontTitle : font;

               // measure current line size
               SizeF lineSize = grfx.MeasureString( textLines[i], currFont );

               yLocCenter = textRect.Y + lineHeight * i + lineCenter;

               float x = xLocCenter - lineSize.Width / 2f;
               float y = yLocCenter - lineSize.Height / 2f;

               grfx.DrawString( textLines[i], currFont, brush, x, y );
            }

            fontTitle.Dispose();
            font.Dispose();
         }

         brush.Dispose();
      }

      protected override void OnResize( EventArgs e )
      {
         base.OnResize( e );
         this.Invalidate();
      }

      protected override void OnMouseDown( MouseEventArgs e )
      {
         if( CheckInputTextPopUp() )
            return;

         Rectangle puzzRect = GetPuzzleRect();

         if( ( e.Button & MouseButtons.Left ) > 0
            && puzzRect.Contains( e.Location ) )
         {
            ++this.moves;

            int tileX = puzzRect.Width / this.size;
            int tileY = puzzRect.Height / this.size;

            int clickX = ( e.X - puzzRect.X ) / tileX;
            int clickY = ( e.Y - puzzRect.Y ) / tileY;

            bool validMove = ShiftTiles( clickX, clickY );

            if( !validMove )
            {
               int curPos = GetCursor();
               int curX = curPos % this.size;
               int curY = curPos / this.size;
               if( clickX == curX && clickY == curY )
               {
                  // show menu
                  this.animationHandler.Start();
                  return;
               }
            }

            this.Invalidate();
            CheckSolved();
         }
      }

      protected override void OnKeyDown( KeyEventArgs e )
      {
         if( CheckInputTextPopUp() )
            return;

         int curPos = GetCursor();

         int curX = curPos % this.size;
         int curY = curPos / this.size;

         bool validShift = false;

         switch( e.KeyCode )
         {
            case Keys.Left:
               if( curX < this.size - 1 )
               {
                  ++curX;
                  validShift = true;
               }
               break;
            case Keys.Right:
               if( curX > 0 )
               {
                  --curX;
                  validShift = true;
               }
               break;
            case Keys.Up:
               if( curY < this.size - 1 )
               {
                  ++curY;
                  validShift = true;
               }
               break;
            case Keys.Down:
               if( curY > 0 )
               {
                  --curY;
                  validShift = true;
               }
               break;

            case Keys.Space:
            case Keys.Escape:
               // show menu
               this.animationHandler.Start();
               break;

            case Keys.A:
               this.maintainAspect = !this.maintainAspect;
               this.Invalidate();
               break;

            case Keys.R:
               // "shift" current image, if modifier key is pressed
               if( e.Shift )
               {
                  int imageCount = this.imageNames.Length;
                  // increment current image index
                  this.currImageIndex = ( this.currImageIndex + 1 ) % imageCount;
               }
               // reset puzzle with current logo type
               CreatePuzzle( this.size );
               this.Invalidate();
               break;

            case Keys.D3:
            case Keys.D4:
            case Keys.D5:
            case Keys.D6:
            case Keys.D7:
            case Keys.D8:
            case Keys.D9:
               CreatePuzzle( 3 + ( (int)e.KeyCode - (int)Keys.D3 ) );
               Shuffle( this.size * this.size * this.size * this.size );
               this.Invalidate();
               break;
            case Keys.Oemtilde:
               CreatePuzzle( 10 );
               Shuffle( this.size * this.size * this.size * this.size );
               this.Invalidate();
               break;
            case Keys.N:
               if( this.image != null )
                  this.numbers = !this.numbers;
               this.Invalidate();
               break;
         }

         if( validShift )
         {
            ++this.moves;
            ShiftTiles( curX, curY );
            this.Invalidate();
            CheckSolved();
         }
      }

      private int GetCursor()
      {
         int curr = 0;
         for( int i = 0; i < this.size * this.size; ++i )
         {
            if( tiles[i % this.size, i / this.size] < 0 )
            {
               curr = i;
               break;
            }
         }
         return curr;
      }

      private bool ShiftTiles( int x, int y )
      {
         bool shifted = false;

         // ensure valid shift positions were given, within bounds of tile array
         if( x < 0 || y < 0 || x >= this.size || y >= this.size )
            return false; // no move, invalid click point... don't click there!

         int curPos = GetCursor();

         int curX = curPos % this.size;
         int curY = curPos / this.size;

         if( ( curX == x ) ^ ( curY == y ) )
         {
            // vertical shift
            if( y < curY )
            {
               for( int mv = curY; mv > y; --mv )
                  tiles[curX, mv] = tiles[curX, mv - 1];
            }
            else if( y > curY )
            {
               for( int mv = curY; mv < y; ++mv )
                  tiles[curX, mv] = tiles[curX, mv + 1];
            }
            // horizontal shift
            if( x < curX )
            {
               for( int mv = curX; mv > x; --mv )
                  tiles[mv, curY] = tiles[mv - 1, curY];
            }
            else if( x > curX )
            {
               for( int mv = curX; mv < x; ++mv )
                  tiles[mv, curY] = tiles[mv + 1, curY];
            }
            shifted = true;
            tiles[x, y] = -1; // update cursor
         }

         return shifted;
      }

      private void CheckSolved()
      {
         bool solveCheck = true;

         int prev = 0;
         for( int i = 1; i < this.size * this.size - 1; ++i )
         {
            solveCheck &= tiles[i % this.size, i / this.size] > tiles[prev % this.size, prev / this.size];
            if( !solveCheck )
               break;
            prev = tiles[i % this.size, i / this.size];
         }

         // set current solve state
         this.solved = solveCheck;

         if( this.solved && this.scrambled )
         {
            // clear scramlbed state -- only allow solve "event" once
            this.scrambled = false;

            float elapsedSeconds = ( Environment.TickCount - this.startTime ) / 1000f;

            // set solved text lines display
            this.textLines = new string[]
               {
                  "Solved!",
                  //"",
                  // (puzzle name/caption..?)
                  //$"#{this.currImageIndex + 1}",
                  //$"Time: {elapsedSeconds:F2}s",
                  string.Format( "Time: {0:F2}s", elapsedSeconds ),
                  //$"Moves: {this.moves}",
                  string.Format( "Moves: {0}", this.moves ),
               };
         }
      }

      private void InitializeContextMenu()
      {
         this.contextMenu = new ContextMenu();

         this.contextMenu.MenuItems.Add( "Pause", new EventHandler( ContextMenuHandler ) );


         this.contextMenu.MenuItems.Add( "-" );
         this.contextMenu.MenuItems.Add( "Help", new EventHandler( ContextMenuHandler ) );

         this.contextMenu.MenuItems.Add( "-" );
         this.contextMenu.MenuItems.Add( "MetalHelix.com", new EventHandler( ContextMenuHandler ) );

         this.ContextMenu = this.contextMenu;
      }

      private void ContextMenuHandler( object sender, EventArgs e )
      {
         MenuItem item = (MenuItem)sender;
         switch( item.Text )
         {
            case "Pause":
               MessageBox.Show( "TODO: Pause" );
               break;

            case "Help":
               MessageBox.Show( "RTFM.\nWait, this is the manual... Sorry." );
               break;

            case "MetalHelix.com":
               //System.Diagnostics.Process.Start( "explorer", "http://www.metalhelix.com" );
               System.Diagnostics.Process.Start( "http://www.metalhelix.com" );
               //System.Windows.Forms.Help.ShowHelp( this, "http://www.metalhelix.com" );

               //System.Windows.Forms.LinkLabel
               //System.Net.WebClient wc = new System.Net.WebClient();
               //wc.DownloadFile( "http://www.metalhelix.com/test/dynpage/dynpage.php", "temp.txt" );
               //object what = System.Windows.Forms.OSFeature.LayeredWindows;
               //System.Windows.Forms.Screen
               //System.Net.WebRequest webReq = System.Net.WebRequest.Create( "MetalHelix.com" );

               //System.Web.Mail ... add ref to System.Web
               //System.Web.Mail.SmtpMail.Send( 

               break;
         }
      }

      /// <summary> The main entry point for the application. </summary>
      [STAThread]
      static void Main()
      {
         Application.Run( new MainForm() );
      }
   }
}
