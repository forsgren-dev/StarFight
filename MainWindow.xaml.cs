using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
// By Erik Forsgren 2025
namespace StarFight
{
    public partial class MainWindow : Window
    {
        private MediaPlayer backgroundMusicPlayer;
        private DispatcherTimer gameTimer = new DispatcherTimer();
        private DispatcherTimer fireTimer = new DispatcherTimer();
        private const double scrollSpeed = 6;
        private double mouseX = 0;
        private const double playerPaddingBottom = 50;
        private const double playerWidth = 100;
        private int enemies = 8;
        private List<Rectangle> bullets = new List<Rectangle>();
        private const double bulletSpeed = 10;
        private const double bulletWidth = 5;
        private const double bulletHeight = 10;
        private bool isFiring = false;
        private const int fireIntervalMs = 400; // Time (ms) between shots
        private List<Particle> particles = new List<Particle>();
        private List<Image> aliens = new List<Image>();
        private Point leaderPosition;
        private double leaderTime = 5;
        private double currentHorizontalRadius;
        private double currentVerticalRadius;
        private double waveVerticalOffset = 0; // Wave vertical movement
        private double waveSpeed = 100;         // Wave vertical start speed
        private double waveSpeedIncrease = 1; // Speed Increase per second
        private double totalTime = 0;
        private DispatcherTimer alienFireTimer = new DispatcherTimer();
        private List<Rectangle> alienBullets = new List<Rectangle>();
        private const double alienBulletSpeed = 10;
        private int score = 0;
        private int wave = 0;
        private bool isWaveTypeW = false;
        private bool isWaveTypeX = false;
        private Random rand = new Random();
        private int playerHealth = 3;   // Ship shield
        private int playerLives = 3;    // Number of ships
        private bool isPlayerAlive = true;
        private bool isInvulnerable = false; //Immunity after respawn
        private DispatcherTimer invulnerableTimer;
        private bool isGameOverWaiting = false;
        private DateTime lastUpdate = DateTime.Now;
        private double deltaTime = 0.016; // Set to 60 FPS (TODO: update in GameLoop)
        private const double startScreenSpeed = 2.0; // Start screen scroll speed
        private double currentscrollSpeed;
        private bool isGameRunning = false;
        private int highScore = 0;
        private int shotIndex = 0;
        // Sounds preloading
        private readonly List<MediaPlayer> shotPlayers = new();
        private Uri shotUri;
        private Uri explodeUri;
        private readonly List<MediaPlayer> explodePlayers = new();
        private int explodeIndex = 0;
        private MediaPlayer playerExplodePlayer;
        public MainWindow()
        {
            InitializeComponent();
            StartBackgroundMusic();
            //Window positioning 
            this.MaxWidth = 1920;
            this.MaxHeight = 1080;
            this.WindowStartupLocation = WindowStartupLocation.Manual;
            this.Loaded += (s, e) =>
            {
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;
                this.Width = Math.Min(screenWidth, 1920);
                this.Height = Math.Min(screenHeight, 1080);
                this.Left = (screenWidth - this.Width) / 2;
                this.Top = (screenHeight - this.Height) / 2;
            };
            shotUri = new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "shot.wav"), UriKind.Absolute);
            for (int i = 0; i < 8; i++)
            {
                // Volume is set when calling PlayShotSound! Should be 0 here.
                var p = new MediaPlayer { Volume = 0.0 }; 
                p.Open(shotUri);
                p.Stop();
                p.MediaEnded += (s, e) => { p.Stop(); p.Position = TimeSpan.Zero; };
                p.MediaFailed += (s, e) => { p.Close(); }; // Error cleanup
                shotPlayers.Add(p);
            }
            explodeUri = new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
    "xplode.mp3"), UriKind.Absolute);

            for (int i = 0; i < 4; i++)
            {
                // Volume is set when calling PlayExplosionSound! Should be 0 here.
                var p = new MediaPlayer { Volume = 0.0 }; 
                p.Open(explodeUri);
                p.Stop();
                p.MediaEnded += (s, e) => { p.Stop(); p.Position = TimeSpan.Zero; };
                p.MediaFailed += (s, e) => { p.Close(); };
                explodePlayers.Add(p);
            }
            // Preloading the player explosion sound
            playerExplodePlayer = new MediaPlayer();
            var uri = new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "player_explosion.mp3"), UriKind.Absolute);
            playerExplodePlayer.Open(uri);
            playerExplodePlayer.Volume = 0.0; // Explosion volume is set in PlayerExplode!
            playerExplodePlayer.Stop();       // Vol 0 here prevents glitches at game start
            playerExplodePlayer.Position = TimeSpan.Zero;
            // Show backgrounds
            Canvas.SetTop(Background1, 0);
            Canvas.SetTop(Background2, -Background2.Height);
            Canvas.SetLeft(Background1, 0);
            Canvas.SetLeft(Background2, 0);
            Canvas.SetTop(Nebulosa, 0);
            Canvas.SetLeft(Nebulosa, 0);
            gameTimer.Interval = TimeSpan.FromMilliseconds(16);
            gameTimer.Tick += GameLoop;
            gameTimer.Start();
            alienFireTimer.Interval = TimeSpan.FromSeconds(2); // Alien fire rate
            alienFireTimer.Tick += AlienFireTimer_Tick;
            fireTimer.Interval = TimeSpan.FromMilliseconds(fireIntervalMs);
            fireTimer.Tick += (s, e) =>
            {
                if (isFiring)
                    ShootBullet();
            };
            invulnerableTimer = new DispatcherTimer();
            invulnerableTimer.Interval = TimeSpan.FromSeconds(3);
            invulnerableTimer.Tick += (s, e) =>
            {
                isInvulnerable = false;
                Player.Opacity = 1;
                invulnerableTimer.Stop();
            };
            GameCanvas.Loaded += (s, e) =>
            {
                mouseX = GameCanvas.ActualWidth / 2;
                PositionPlayer(mouseX);
            };
            GameCanvas.SizeChanged += (s, e) => ApplyParallax();
            this.MouseMove += Window_MouseMove;
            this.MouseDown += Window_MouseDown;
            this.MouseUp += Window_MouseUp;
            this.KeyDown += Window_KeyDown;
            this.Focusable = true;
            this.Focus();
            // Slower background in start screen
            currentscrollSpeed = startScreenSpeed;
            // Hide player in start screen
            Player.Visibility = Visibility.Collapsed;
        }
        private void GameLoop(object sender, EventArgs e)
        {
            MoveBackground(Background1);
            MoveBackground(Background2);
            MoveBackground(Nebulosa);
            UpdateBullets();
            UpdateAlienBullets();
            UpdateParticles();
            ApplyParallax();
            // Update alien movement in Game Over
            if (aliens.Count > 0)
            {
                if (isWaveTypeW)
                    UpdateAliensMovementW();
                else
                    UpdateAliensMovement();
            }
            // If Game Over do nothing else
            if (!isGameRunning && isGameOverWaiting)
                return;
            // Extra score if all aliens killed
            bool allAliensDead = aliens.All(a => a.Opacity == 0);
            if (allAliensDead)
            {
                score += wave * 1000;
                ScoreText.Text = $"Score: {score:N0}";
            }
            // Check if aliens are below player
            double playerY = Canvas.GetTop(Player);
            bool allAliensBelowPlayer = aliens.All(alien =>
            {
                double alienY = Canvas.GetTop(alien);
                return alienY > playerY + Player.ActualHeight + 200;
            });
            // Spawn new wave - add up to 50 aliens
            if (allAliensDead || allAliensBelowPlayer)
            {
                if (enemies < 50)
                    enemies++;
                SpawnNewWave(enemies);
            }
        }
        private void AlienFireTimer_Tick(object sender, EventArgs e)
        {
            var aliveAliens = aliens.Where(a => a.Opacity > 0).ToList();
            if (aliveAliens.Count == 0) return;
            // Random alien to shoot
            var shooter = aliveAliens[rand.Next(aliveAliens.Count)];
            double alienX = Canvas.GetLeft(shooter) + shooter.Width / 2;
            double alienY = Canvas.GetTop(shooter) + shooter.Height;
            ShootAlienBullet(alienX, alienY);
        }
        private void ShootAlienBullet(double x, double y)
        {
            Rectangle bullet = new Rectangle
            {
                Width = bulletWidth,
                Height = bulletHeight,
                Fill = Brushes.Red
            };
            Canvas.SetLeft(bullet, x - bulletWidth / 2);
            Canvas.SetTop(bullet, y - 15); // 15 closer to alien

            GameCanvas.Children.Add(bullet);
            alienBullets.Add(bullet);
        }
        private void RespawnPlayer()
        {
            playerHealth = 3;
            ShieldText.Text = $"Shield: {playerHealth}";
            isPlayerAlive = true;
            Player.Visibility = Visibility.Visible;

            /* Tested respawning the player centered on screen
               I think it looks better not to do this
               mouseX = GameCanvas.ActualWidth / 2;
               PositionPlayer(mouseX); */

            // Start immunity at respawn
            isInvulnerable = true;
            Player.Opacity = 0.5;
            invulnerableTimer.Stop(); // Stop timer if running
            invulnerableTimer.Start();
        }
        private void GameOver()
        {
            isPlayerAlive = false;
            isGameRunning = false;
            Player.Visibility = Visibility.Collapsed;
            currentscrollSpeed = startScreenSpeed;
            isGameOverWaiting = true;
            leaderTime = 5;

            // Delay to let animations finish
            DispatcherTimer showScoreTimer = new DispatcherTimer();
            showScoreTimer.Interval = TimeSpan.FromSeconds(0.5);
            showScoreTimer.Tick += (s, e) =>
            {
                showScoreTimer.Stop();
                ScoreText.Text = $"Score: {score:N0}";
                StartScreen.Visibility = Visibility.Visible;
                WaveText.Visibility = Visibility.Collapsed;
                ScoreText.Visibility = Visibility.Visible;
                LivesText.Visibility = Visibility.Collapsed;
                ShieldText.Visibility = Visibility.Collapsed;
                if (score > highScore)
                {
                    highScore = score;
                    HighScoreText.Text = $"Highscore: {highScore:N0}";
                }
                HighScoreText.Visibility = Visibility.Visible;
                this.Cursor = Cursors.Arrow; // Show mouse cursor
            };
            showScoreTimer.Start();
            // Reset wavespeeds
            waveSpeedIncrease = 1;
            waveSpeed = 100;
        }
        private void UpdateAlienBullets()
        {
            for (int i = alienBullets.Count - 1; i >= 0; i--)
            {
                var bullet = alienBullets[i];
                double x = Canvas.GetLeft(bullet);
                double y = Canvas.GetTop(bullet) + alienBulletSpeed;
                Canvas.SetTop(bullet, y);
                if (y > GameCanvas.ActualHeight)
                {
                    GameCanvas.Children.Remove(bullet);
                    alienBullets.RemoveAt(i);
                    continue;
                }
                var bulletRect = new Rect(Canvas.GetLeft(bullet), y,
                    bulletWidth, bulletHeight);
                var playerRect = new Rect(Canvas.GetLeft(Player), Canvas.GetTop(Player),
                    Player.ActualWidth, Player.ActualHeight);
                if (!isInvulnerable && bulletRect.IntersectsWith(playerRect))
                {
                    GameCanvas.Children.Remove(bullet);
                    alienBullets.RemoveAt(i);
                    if (isPlayerAlive)
                    {
                        playerHealth--;
                        ShieldText.Text = $"Shield: {playerHealth}";
                        if (playerHealth <= 0)
                        {
                            PlayerExplode();
                        }
                        else
                        {
                            _ = BlinkPlayerAsync(); // Catch own exceptions
                        }
                    }
                }
            }
        }
        private async Task BlinkPlayerAsync()
        {
            try
            {
                for (int b = 0; b < 5; b++)
                {
                    Player.Opacity = 0.5;
                    await Task.Delay(100);
                    Player.Opacity = 1;
                    await Task.Delay(100);
                }
            }
            catch { } // Ignore exceptions if game ends during blinking
        }
        private void UpdateParticles()
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                particles[i].Update(deltaTime);
                if (particles[i].IsDead)
                {
                    GameCanvas.Children.Remove(particles[i].Shape);
                    particles.RemoveAt(i);
                }
            }
        }
        private async void PlayerExplode()
        {
            isPlayerAlive = false;
            double playerX = Canvas.GetLeft(Player) + Player.ActualWidth / 2;
            double playerY = Canvas.GetTop(Player) + Player.ActualHeight / 2;
            // Explosion
            CreateExplosion(playerX, playerY);
            // Player explosion sound
            try
            {
                playerExplodePlayer.Volume = 0.6; // Explosion volume
                playerExplodePlayer.Stop();
                playerExplodePlayer.Position = TimeSpan.Zero;
                playerExplodePlayer.Play();
            }
            catch { } // Ignore sound errors
            // Hide ship
            Player.Visibility = Visibility.Collapsed;
            // Remove life
            playerLives--;
            LivesText.Text = $"Lives: {playerLives}";
            // Check for Game Over
            if (playerLives > 0)
            {
                // Delayed respawn
                DispatcherTimer respawnTimer = new DispatcherTimer();
                respawnTimer.Interval = TimeSpan.FromSeconds(2);
                respawnTimer.Tick += (s, e) =>
                {
                    respawnTimer.Stop();
                    RespawnPlayer();
                };
                respawnTimer.Start();
            }
            else
            {
                // Delay at Game Over
                await Task.Delay(500);
                GameOver();
            }
        }
        private void PlayExplosionSound()
        {
            if (explodePlayers.Count == 0) return;
            var p = explodePlayers[explodeIndex];
            explodeIndex = (explodeIndex + 1) % explodePlayers.Count;
            try
            {
                p.Stop();
                p.Position = TimeSpan.Zero;
                p.Volume = 0.7;
                p.Play();
            }
            catch
            {
                p.Close();
                p.Open(explodeUri);
                p.Play();
            }
        }
        private void CreateExplosion(double x, double y)
        {
            int particleCount = 20;
            for (int i = 0; i < particleCount; i++)
            {
                // Random explotion angle
                double angle = rand.NextDouble() * 2 * Math.PI;
                // Random explotion speed
                double speed = 20 + rand.NextDouble() * 40;

                Vector velocity = new Vector(Math.Cos(angle) *
                    speed, Math.Sin(angle) * speed);

                Color color = Color.FromRgb(
                    (byte)(200 + rand.Next(56)),
                    (byte)(rand.Next(100)),
                    (byte)(rand.Next(20))
                );
                Ellipse shape = new Ellipse
                {
                    Width = 3, // Mindre
                    Height = 3,
                    Fill = new SolidColorBrush(color),
                    Opacity = 1
                };
                Canvas.SetLeft(shape, x);
                Canvas.SetTop(shape, y);
                // Particle lifetime
                Particle particle = new Particle(x, y, velocity, lifeTime: 0.4);
                particles.Add(particle);
                GameCanvas.Children.Add(particle.Shape);
            }
        }
        private void SpawnNewWave(int count)
        {
            waveVerticalOffset = 100;
            totalTime = 0;
            leaderTime = 0;
            if (isGameRunning)
                wave++;
            WaveText.Text = $"Wave: {wave}";
            // Attack wave vertical speed (increasing per wave)
            double baseSpeed = 100;              // First wave base speed
            double waveSpeedPerWave = 0.3;       // Increase per wave
            waveSpeed = baseSpeed + waveSpeedPerWave * (wave - 1);
            // Acceleration during wave
            double baseAcceleration = 0.2;      // First wave acceleration
            double accelerationPerWave = 0.1;  // Per wave increase
            waveSpeedIncrease += baseAcceleration + accelerationPerWave * (wave - 1);
            // WaveType W
            isWaveTypeW = (wave % 5 == 0);
            // WaveType X
            isWaveTypeX = (wave % 2 == 0);
            // Faster interval per wave
            alienFireTimer.Interval = TimeSpan.FromSeconds(Math.Max(0.5, 1.5 - wave * 0.02));
            // Spawn aliens (remove old)
            foreach (var alien in aliens)
                GameCanvas.Children.Remove(alien);
            aliens.Clear();
            for (int i = 0; i < count; i++)
            {
                Image alien = new Image
                {
                    Width = 70,
                    Height = 70,
                    Stretch = Stretch.Fill,
                    Source = new BitmapImage(new Uri(isWaveTypeW ?
                        "pack://application:,,,/images/alien2.png" : 
                        isWaveTypeX ?
                        "pack://application:,,,/images/alien3.png" :
                        "pack://application:,,,/images/alien1.png"))
                };
                double x = rand.Next(0, (int)(GameCanvas.ActualWidth - alien.Width));
                double y = -300;
                Canvas.SetLeft(alien, x);
                Canvas.SetTop(alien, y);
                aliens.Add(alien);
                GameCanvas.Children.Add(alien);
            }
            // Random wave width
            currentHorizontalRadius = 100 + rand.NextDouble() * (GameCanvas.ActualWidth / 2);
            // Random wave height
            currentVerticalRadius = 40 + (rand.NextDouble() * 140);
            // Reset alien movement
            leaderTime = 1;
            waveVerticalOffset = 50;
            totalTime = 0;
        }
        private void UpdateAliensMovement()
        {
            totalTime += deltaTime;
            leaderTime += deltaTime * 2;
            // Vertical acceleration
            waveVerticalOffset += waveSpeed * deltaTime;
            waveSpeed += waveSpeedIncrease * deltaTime;
            if (isWaveTypeW)
            {
                UpdateAliensMovementW();
                return;
            }
            for (int i = 0; i < aliens.Count; i++)
            {
                Point pos = GetAlienPosition(i);
                double x = pos.X;
                double y = pos.Y; // waveVerticalOffset set in GetAlienPosition

                Canvas.SetLeft(aliens[i], x - aliens[i].Width / 2); // Center
                Canvas.SetTop(aliens[i], y);
            }
            // Player collision check (rect heights reduced)
            if (isPlayerAlive && Player.Visibility == Visibility.Visible)
            {
                double playerX = Canvas.GetLeft(Player);
                double playerY = Canvas.GetTop(Player);
                Rect playerRect = new Rect(playerX, playerY, Player.ActualWidth, Player.ActualHeight - 20);

                foreach (var alien in aliens)
                {
                    if (alien.Opacity == 0) continue;
                    double alienX = Canvas.GetLeft(alien);
                    double alienY = Canvas.GetTop(alien);
                    Rect alienRect = new Rect(alienX, alienY, alien.Width, alien.Height - 20);
                    if (!isInvulnerable && playerRect.IntersectsWith(alienRect))
                    {
                        PlayerExplode();
                        break;
                    }
                }
            }
        }
        private void UpdateAliensMovementW()
        {
            totalTime += deltaTime;
            waveVerticalOffset += waveSpeed * deltaTime;
            waveSpeed += waveSpeedIncrease * deltaTime;
            double centerX = GameCanvas.ActualWidth / 2;
            double horizontalAmplitude = (GameCanvas.ActualWidth - 100) / 2;
            double verticalAmplitude = 100 + (wave * 4);
            for (int i = 0; i < aliens.Count; i++)
            {
                double waveOffset = i * 0.5;
                double x = centerX + horizontalAmplitude * Math.Sin(totalTime * 1.5 + waveOffset) *
                           Math.Abs(Math.Sin(totalTime + waveOffset));
                double yWave = verticalAmplitude * Math.Sin(totalTime * 4 + waveOffset);
                double y = -300 + waveVerticalOffset + yWave; // Startposition
                Canvas.SetLeft(aliens[i], x - aliens[i].Width / 2);
                Canvas.SetTop(aliens[i], y);
            }
            if (isPlayerAlive && Player.Visibility == Visibility.Visible)
            {
                double playerX = Canvas.GetLeft(Player);
                double playerY = Canvas.GetTop(Player);
                Rect playerRect = new Rect(playerX, playerY,
                    Player.ActualWidth, Player.ActualHeight - 20);
                foreach (var alien in aliens)
                {
                    if (alien.Opacity == 0) continue;

                    double alienX = Canvas.GetLeft(alien);
                    double alienY = Canvas.GetTop(alien);
                    Rect alienRect = new Rect(alienX, alienY,
                        alien.Width, alien.Height - 20);
                    if (!isInvulnerable && playerRect.IntersectsWith(alienRect))
                    {
                        PlayerExplode();
                        break;
                    }
                }
            }
        }
        private void PositionPlayer(double mouseX)
        {
            double canvasWidth = GameCanvas.ActualWidth;
            double canvasHeight = GameCanvas.ActualHeight;
            double playerWidth = Player.ActualWidth;
            double playerHeight = Player.ActualHeight - 20;
            // Set width and height to default
            if (playerWidth == 0) playerWidth = 100;
            if (playerHeight == 0) playerHeight = 100;
            // X is in the middle of the player ship
            double x = mouseX - playerWidth / 2;
            if (x < 0) x = 0;
            if (x > canvasWidth - playerWidth) x = canvasWidth - playerWidth;
            double y = Height - playerPaddingBottom - playerHeight;
            // Set player position
            Canvas.SetLeft(Player, x);
            Canvas.SetTop(Player, y);
        }
        private void MoveBackground(System.Windows.Controls.Image bg)
        {
            double y = Canvas.GetTop(bg);
            y += currentscrollSpeed;
            if (y >= Height)
                y = -bg.Height;
            Canvas.SetTop(bg, y);
        }
        private void ApplyParallax()
        {
            if (isGameRunning)
            {
                double centerX = GameCanvas.ActualWidth / 2;
                double offsetX = (centerX - mouseX) / centerX;
                double backgroundWidth = Background1.ActualWidth;
                double canvasWidth = GameCanvas.ActualWidth;
                // Wait until canvas is set
                if (backgroundWidth == 0 || canvasWidth == 0)
                    return; 
                // Set nebulosa background position
                double maxHorizontalShift = backgroundWidth - canvasWidth;
                double spillRoom = 0; // For testing
                double minLeft = -maxHorizontalShift - spillRoom;
                double maxLeft = spillRoom;
                double parallaxRange = maxLeft - minLeft;
                double normalized = (offsetX + 1) / 2;
                double leftPos = minLeft + normalized * parallaxRange;
                if (leftPos > maxLeft) 
                    leftPos = maxLeft;
                if (leftPos < minLeft) 
                    leftPos = minLeft;
                Canvas.SetLeft(Background1, leftPos);
                Canvas.SetLeft(Background2, leftPos);
                Canvas.SetLeft(Nebulosa, leftPos * 2); //Movement relative to background
            }
        }
        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(GameCanvas);
            mouseX = pos.X;
            // Set position only if game is running
            if (isGameRunning)
                PositionPlayer(mouseX);
        }
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!isGameRunning) return;
            if (!isPlayerAlive) return;

            if (e.ChangedButton == MouseButton.Left)
            {
                isFiring = true;
                fireTimer.Start();
                ShootBullet();
            }
        }
        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!isGameRunning) 
                return; // Ignore if in start screen
            // Stop firing on button release
            if (e.ChangedButton == MouseButton.Left)
            {
                isFiring = false;
                fireTimer.Stop();
            }
        }
        private void PlayShotSound(double volume)
        {
            if (shotPlayers.Count == 0) return;
            var p = shotPlayers[shotIndex];
            shotIndex = (shotIndex + 1) % shotPlayers.Count;
            // Restart mediaplayer if any trouble
            try
            {
                p.Stop();
                p.Position = TimeSpan.Zero;
                p.Volume = volume;
                p.Play();
            }
            catch
            {
                p.Close();
                p.Open(shotUri);
                p.Play();
            }
        }
        private void ShootBullet()
        {
            PlayShotSound(0.2);
            Rectangle bullet = new Rectangle
            {
                Width = bulletWidth,
                Height = bulletHeight,
                Fill = Brushes.Yellow
            };
            double playerX = Canvas.GetLeft(Player);
            double playerY = Canvas.GetTop(Player);
            double playerWidth = Player.ActualWidth;
            // Set bullet position in the middle of player
            double bulletX = playerX + playerWidth / 2 - bulletWidth / 2;
            Canvas.SetLeft(bullet, bulletX);
            Canvas.SetTop(bullet, playerY + 20); // 20 closer to ship
            // Fire shot
            GameCanvas.Children.Add(bullet);
            bullets.Add(bullet);
        }
        private void UpdateBullets()
        {
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                var bullet = bullets[i];
                double bulletX = Canvas.GetLeft(bullet);
                double bulletY = Canvas.GetTop(bullet);
                Rect bulletRect = new Rect(bulletX, bulletY, bulletWidth, bulletHeight);
                bool hit = false;
                for (int j = 0; j < aliens.Count; j++)
                {
                    var alien = aliens[j];
                    // Skip dead aliens
                    if (alien.Opacity == 0)
                        continue;
                    // Check collision
                    double alienX = Canvas.GetLeft(alien);
                    double alienY = Canvas.GetTop(alien);
                    Rect alienRect = new Rect(alienX, alienY, alien.Width, alien.Height);
                    if (bulletRect.IntersectsWith(alienRect))
                    {
                        CreateExplosion(alienX + alien.Width / 2, alienY + alien.Height / 2);
                        PlayExplosionSound();
                        // Hide alien
                        alien.Opacity = 0;
                        score += 100; // Points per alien
                        ScoreText.Text = $"Score: {score:N0}";
                        // Remove bullet
                        GameCanvas.Children.Remove(bullet);
                        bullets.RemoveAt(i);
                        hit = true;
                        break;
                    }
                }
                // Remove bullet at top of screen
                if (!hit)
                {
                    double y = bulletY - bulletSpeed;
                    if (y < -bulletHeight)
                    {
                        GameCanvas.Children.Remove(bullet);
                        bullets.RemoveAt(i);
                    }
                    else
                    {
                        Canvas.SetTop(bullet, y);
                    }
                }
            }
        }
        // Calculate alien position
        private Point GetAlienPosition(int i)
        {
            double t = leaderTime; // total tid
            double phase = leaderTime - i * 0.2; // Distance between aliens
            double x = GameCanvas.ActualWidth / 2 + currentHorizontalRadius * Math.Sin(phase);
            double yWave = currentVerticalRadius * Math.Sin(phase) * Math.Cos(phase); // 8 formation
            double y = -200 + waveVerticalOffset + yWave; // vertical movement plus 8 formation
            return new Point(x, y);
        }
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.WindowStyle = WindowStyle.SingleBorderWindow;
                this.WindowState = WindowState.Normal;
                this.Topmost = false;
            }
            else if (e.Key == Key.F11)
            {
                this.WindowStyle = WindowStyle.None;
                this.WindowState = WindowState.Maximized;
                this.Topmost = true;
            }
        }
        private void StartGame_Click(object sender, RoutedEventArgs e) // Start button events
        {
            // Fade-out animation 
            var fade = new DoubleAnimation(1.0, 0.0, TimeSpan.FromSeconds(0.9));
            fade.FillBehavior = FillBehavior.Stop;
            fade.Completed += (s, ev) =>
            {
                // Hide start screen
                StartScreen.Visibility = Visibility.Collapsed;
                // Reset stats if game over
                if (isGameOverWaiting)
                {
                    playerLives = 3;
                    playerHealth = 3;
                    score = 0;
                    ScoreText.Text = $"Score: {score:N0}";
                    wave = 0;
                    isGameOverWaiting = false;
                    enemies = 8;
                }
                // Show player
                Player.Visibility = Visibility.Visible;
                // Reset game status
                isPlayerAlive = true;
                isInvulnerable = false;
                Player.Opacity = 1;
                invulnerableTimer.Stop();
                enemies = 8;
                isGameRunning = true;
                this.Cursor = Cursors.None;
                currentscrollSpeed = scrollSpeed;
                // Show UI
                WaveText.Visibility = Visibility.Visible;
                ScoreText.Visibility = Visibility.Visible;
                LivesText.Text = $"Lives: {playerLives}";
                LivesText.Visibility = Visibility.Visible;
                ShieldText.Text = $"Shield: {playerHealth}";
                ShieldText.Visibility = Visibility.Visible;
                HighScoreText.Text = $"Highscore: {highScore:N0}";
                HighScoreText.Visibility = Visibility.Visible;
                // Start alien shot timer
                alienFireTimer.Start();
                // Start new wave
                SpawnNewWave(enemies);
            };
            StartScreen.BeginAnimation(UIElement.OpacityProperty, fade);
        }
        public void StartBackgroundMusic()
        {
            backgroundMusicPlayer = new MediaPlayer();
            var musicPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "music.mp3");
            backgroundMusicPlayer.Open(new Uri(musicPath));
            backgroundMusicPlayer.MediaEnded += (s, e) =>
            {
                backgroundMusicPlayer.Position = TimeSpan.Zero;  // Rewind
                backgroundMusicPlayer.Play();                    // Restart music
            };
            backgroundMusicPlayer.Volume = 0.6; // Music volume
            backgroundMusicPlayer.Play();
        }
        class Particle
        {
            public Ellipse Shape;
            public Vector Velocity;
            public double LifeTime;
            private double MaxLifeTime;
            private const double FadeMultiplier = 1.5; // Fade factor
            public Particle(double x, double y, Vector velocity, double lifeTime = 0.4)
            {
                double size = 5.5; // Particle diameter

                Shape = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = Brushes.OrangeRed,
                    Opacity = 1
                };
                Canvas.SetLeft(Shape, x);
                Canvas.SetTop(Shape, y);
                Velocity = velocity;
                MaxLifeTime = lifeTime;
                LifeTime = MaxLifeTime;
            }
            public void Update(double deltaTime)
            {
                LifeTime -= deltaTime;
                if (LifeTime < 0) 
                    LifeTime = 0;
                double x = Canvas.GetLeft(Shape);
                double y = Canvas.GetTop(Shape);
                // Movement per second 
                x += Velocity.X * deltaTime * 10;
                y += Velocity.Y * deltaTime * 10;
                Canvas.SetLeft(Shape, x);
                Canvas.SetTop(Shape, y);
                double fade = LifeTime / MaxLifeTime;
                Shape.Opacity = Math.Max(0, fade * (1 / FadeMultiplier));
            }
            public bool IsDead => LifeTime <= 0;
        }

    }
}
