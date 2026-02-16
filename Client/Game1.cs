using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Shared;

namespace Client;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private UdpClient _networkClient;
    private TankState _myTank;
    private Dictionary<byte, TankState> _otherTanks = new Dictionary<byte, TankState>();
    private Texture2D _turretTexture, _hullTexture, _tracksTexture;
    private float _lastSentTurretRotation;
    private List<Bullet> _bullets = new List<Bullet>();
    private MouseState _lastMouseState;
    private Texture2D _bulletTexture;
    private Vector2 _lastSentPosition = Vector2.Zero;
    private float _netSendTimer = 0f;
    private const float NetSendInterval = 0.05f;
    private float _lastSentHullRotation = 0f;
    private float _shootTimer = 0f;
    private const float ShootDelay = 3.0f;
    private const float RecoilStrength = 150f;
    private Vector2 _recoilVelocity = Vector2.Zero;
    private const float RecoilPower = 12f;
    private Texture2D _pixel;
    private SpriteFont _scoreFont;
    private Texture2D _explosionSheet;
    private Animation _explosionAnimation;
    private List<Explosion> _explosions = new List<Explosion>();
    private int MapWidth;
    private int MapHeight;
    private Texture2D _groundTexture;
    private SoundEffect _shotSound, _reloadSound;
    private Matrix _cameraMatrix;
    private float _shakeIntensity = 0f;
    private Random _random = new Random();
    private List<KillMessage> _killFeed = new List<KillMessage>();
    private bool _isEnteringNick = true;
    private string _nickBuffer = "";
    private float _cameraZoom = 1.0f;
    private float _targetZoom = 1.0f;
    private int _previousScrollValue = 0;
    
    private List<Rectangle> _obstacles = new List<Rectangle>();
    private List<string> _mapLines = new List<string>();
    private int _tileSize = 256;
    private Texture2D _wallTexture;

    private string _serverIp;
    private int _serverPort;
    
    public Game1(string ip, int port)
    {
        _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth = 1920;
        _graphics.PreferredBackBufferHeight = 1080;
        _graphics.ApplyChanges();
        
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        _serverIp = ip;
        _serverPort = port;
    }

    protected override void Initialize()
    {
        _networkClient = new UdpClient();
        _networkClient.Connect(_serverIp, _serverPort);
        
        _myTank = null;
        
        Window.TextInput += (s, e) => {
            if (_isEnteringNick)
            {
                if (e.Key == Keys.Back && _nickBuffer.Length > 0)
                    _nickBuffer = _nickBuffer.Remove(_nickBuffer.Length - 1);
                else if (e.Key == Keys.Enter && _nickBuffer.Length > 0)
                {
                    _myTank = new TankState { 
                        Id = (byte)_random.Next(1, 255), 
                        Name = _nickBuffer, 
                        X = _random.Next(GameConstants.MinSpawnX, MapWidth - GameConstants.MinSpawnX), 
                        Y = _random.Next(GameConstants.MinSpawnY, MapHeight - GameConstants.MinSpawnY)
                    };
    
                    _lastSentTurretRotation = _myTank.TurretRotation;

                    byte[] data = _myTank.ToBytes();
                    _networkClient.Send(data, data.Length);

                    UpdateCamera();
                    _isEnteringNick = false;
                }
                else if (e.Character >= 32 && e.Character <= 126 && _nickBuffer.Length < GameConstants.MaxNicknameLength)
                {
                    _nickBuffer += e.Character;
                }
            }
        };
        
        LoadMap("map.txt");

        Task.Run((() => ListenFromServer()));
        
        base.Initialize();
    }
    
    private async Task ListenFromServer()
    {
        while (true)
        {
            try
            {
                var result = await _networkClient.ReceiveAsync();
                if (result.Buffer[0] == 1)
                {
                    var incomingTank = TankState.FromBytes(result.Buffer);
                    if (_otherTanks.ContainsKey(incomingTank.Id))
                    {
                        var existing = _otherTanks[incomingTank.Id];
        
                        existing.TargetX = incomingTank.X;
                        existing.TargetY = incomingTank.Y;
                        existing.TargetHullRotation = incomingTank.HullRotation;
                        existing.TargetTurretRotation = incomingTank.TurretRotation;
        
                        existing.Health = incomingTank.Health;
                        existing.Kills = incomingTank.Kills;
                    }
                    else
                    {
                        incomingTank.TargetX = incomingTank.X;
                        incomingTank.TargetY = incomingTank.Y;
                        incomingTank.TargetHullRotation = incomingTank.HullRotation;
                        incomingTank.TargetTurretRotation = incomingTank.TurretRotation;
                        _otherTanks[incomingTank.Id] = incomingTank;
                    }
                }
                else if (result.Buffer[0] == 2)
                {
                    var incomingBullet = BulletPacket.FromBytes(result.Buffer);
                    SpawnBullet(incomingBullet);
                    _shotSound.Play(0.3f, 0.0f, 0.0f);
                }
                else if (result.Buffer[0] == 3)
                {
                    var incomingDmg = DamagePacket.FromBytes(result.Buffer);
                    
                    if (_myTank == null) continue;
                    
                    string killerName = "UNKNOWN";
                    if (incomingDmg.AttackerId == _myTank.Id) killerName = _myTank.Name;
                    else if (_otherTanks.ContainsKey(incomingDmg.AttackerId)) killerName = _otherTanks[incomingDmg.AttackerId].Name;
                    
                    TankState victim = null;
                    
                    if (incomingDmg.TargetId == _myTank.Id) victim = _myTank;
                    else if (_otherTanks.ContainsKey(incomingDmg.TargetId)) victim = _otherTanks[incomingDmg.TargetId];
                    
                    if (victim != null)
                    {
                        // _explosions.Add(new Explosion { Position = new Vector2(victim.X, victim.Y) }); // TODO nie wiem czy potrzebne
                        if (victim.Health > 0 && victim.Health - incomingDmg.DamageAmount <= 0)
                        {
                            AddToKillFeed(killerName, victim.Name);
                        }
        
                        victim.Health -= incomingDmg.DamageAmount;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                await Task.Delay(100);
            }
        }
    }
    
    private void SpawnBullet(BulletPacket bullet) {
        _bullets.Add(new Bullet { 
            Position = new Vector2(bullet.X, bullet.Y), 
            Rotation = bullet.Rotation,
            PlayerId = bullet.PlayerId,
        });
    }

    private void AddToKillFeed(string killer, string victim)
    {
        _killFeed.Insert(0, new KillMessage { 
            Text = $"{killer} -> {victim}",
            Color = killer == _myTank.Name ? Color.Yellow : Color.White 
        });
    }
    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _hullTexture = Content.Load<Texture2D>("assets/PNG/Hulls_Color_A/Hull_01.png");
        _turretTexture = Content.Load<Texture2D>("assets/PNG/Weapon_Color_A/Gun_01.png");
        _tracksTexture = Content.Load<Texture2D>("assets/PNG/Tracks/Track_1_A.png");
        _bulletTexture = Content.Load<Texture2D>("assets/PNG/Effects/Medium_Shell");
        _shotSound = Content.Load<SoundEffect>("assets/AUDIO/shot");
        _reloadSound = Content.Load<SoundEffect>("assets/AUDIO/reload");
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _scoreFont = Content.Load<SpriteFont>("ScoreFont");
        _explosionSheet = Content.Load<Texture2D>("assets/PNG/Effects/Explosion_merged");
        _explosionAnimation = new Animation(_explosionSheet.Width / 8, _explosionSheet.Height, 8, 75);
        _groundTexture = Content.Load<Texture2D>("assets/Texture/TXTilesetGrass");
        _wallTexture = Content.Load<Texture2D>("assets/Texture/metalbox");
    }
    
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _networkClient?.Dispose();
        }
        base.Dispose(disposing);
    }
    
    

    protected override void Update(GameTime gameTime)
    {
        if (_isEnteringNick || _myTank == null) 
        {
            return; 
        }
        
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
            Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();
        
        var kState = Keyboard.GetState();
        var mState = Mouse.GetState();
        bool hasChanged = false;
        
        int scrollDelta = mState.ScrollWheelValue - _previousScrollValue;
        if (scrollDelta != 0)
        {
            if (scrollDelta > 0) _targetZoom += 0.2f;
            else _targetZoom -= 0.2f;

            _targetZoom = MathHelper.Clamp(_targetZoom, 0.4f, 2.5f); 
        }
        _previousScrollValue = mState.ScrollWheelValue;
        _cameraZoom = MathHelper.Lerp(_cameraZoom, _targetZoom, GameConstants.Smoothing);
        
        UpdateCamera();

        foreach (var other in _otherTanks.Values)
        {
            other.X = MathHelper.Lerp(other.X, other.TargetX, GameConstants.Smoothing);
            other.Y = MathHelper.Lerp(other.Y, other.TargetY, GameConstants.Smoothing);

            other.HullRotation += MathHelper.WrapAngle(other.TargetHullRotation - other.HullRotation) *
                                  GameConstants.Smoothing;
            other.TurretRotation += MathHelper.WrapAngle(other.TargetTurretRotation - other.TurretRotation) *
                                    GameConstants.Smoothing;
        }

        if (_shootTimer > 0)
        {
            _shootTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_shootTimer <= 0)
            {
                _shootTimer = 0;
                _reloadSound.Play(); 
            }
        }
        
        if (mState.LeftButton == ButtonState.Pressed && _lastMouseState.LeftButton == ButtonState.Released && _shootTimer <= 0)
        {
            _shakeIntensity = GameConstants.ShakeIntensityOnShoot;
            Vector2 spawnPos = new Vector2(_myTank.X, _myTank.Y) + 
                               new Vector2((float)Math.Cos(_myTank.TurretRotation - MathHelper.PiOver2), 
                                   (float)Math.Sin(_myTank.TurretRotation - MathHelper.PiOver2)) * GameConstants.BarrelLength;
            
            Vector2 shootDirection = new Vector2(
                (float)Math.Cos(_myTank.TurretRotation - MathHelper.PiOver2),
                (float)Math.Sin(_myTank.TurretRotation - MathHelper.PiOver2)
            );
            
            var shootPkt = new BulletPacket {
                PlayerId = _myTank.Id,
                X = spawnPos.X,
                Y = spawnPos.Y,
                Rotation = _myTank.TurretRotation
            };
            float randomPitch = (float)(_random.NextDouble() * GameConstants.ShotPitchVariation - GameConstants.ShotPitchVariation / 2);
            _shotSound.Play(GameConstants.ShotVolume, randomPitch, 0.0f);
            
            byte[] shootData = shootPkt.ToBytes();
            _networkClient.Send(shootData, shootData.Length);

            SpawnBullet(shootPkt);
            
            _shootTimer = ShootDelay;

            _recoilVelocity = -shootDirection * RecoilPower;

            hasChanged = true;
        }
        _lastMouseState = mState;
        
        for (int i = _bullets.Count - 1; i >= 0; i--)
        {
            var b = _bullets[i];
            b.Position.X += (float)Math.Cos(b.Rotation - MathHelper.PiOver2) * b.Speed;
            b.Position.Y += (float)Math.Sin(b.Rotation - MathHelper.PiOver2) * b.Speed;
            b.Lifetime -= (float)gameTime.ElapsedGameTime.TotalSeconds;

            bool bulletRemoved = false;
            
            if (IsCollidingWithWall(b.Position, 10f))
            {
                _explosions.Add(new Explosion { Position = b.Position });
                _bullets.RemoveAt(i);
                continue;
            }

            // Check collision with all tanks
            List<TankState> allTanks = new List<TankState>(_otherTanks.Values);
            if (_myTank != null) allTanks.Add(_myTank);
            
            foreach (var other in allTanks)
            {
                if (Vector2.Distance(b.Position, new Vector2(other.X, other.Y)) < GameConstants.TankRadius)
                {
                    _explosions.Add(new Explosion { Position = b.Position });
            
                    if (other.Id == _myTank.Id)
                    {
                        _shakeIntensity = GameConstants.ShakeIntensityOnShoot * 0.5f; // Drżenie ekranu gdy oberwiemy
                    }
                    
                    if (b.PlayerId == _myTank.Id && other.Id != _myTank.Id)
                    {
                        if (other.Health <= GameConstants.DamageAmount)
                        {
                            _myTank.Kills++;
                            AddToKillFeed(_myTank.Name, other.Name);
                        }
                        var dmgPkt = new DamagePacket { 
                            TargetId = other.Id, 
                            AttackerId = _myTank.Id,
                            DamageAmount = GameConstants.DamageAmount
                        };
                        byte[] dmgData = dmgPkt.ToBytes();
                        _networkClient.Send(dmgData, dmgData.Length);
                    }
                    
                    _bullets.RemoveAt(i);
                    bulletRemoved = true;
                    break;
                }
            }

            if (!bulletRemoved && b.Lifetime <= 0) 
                _bullets.RemoveAt(i);
        }

        if (kState.IsKeyDown(Keys.A)) 
        { 
            _myTank.HullRotation -= GameConstants.TankRotationSpeed; 
            _myTank.TurretRotation -= GameConstants.TankRotationSpeed;
            hasChanged = true;
        }
        if (kState.IsKeyDown(Keys.D)) 
        { 
            _myTank.HullRotation += GameConstants.TankRotationSpeed; 
            _myTank.TurretRotation += GameConstants.TankRotationSpeed;
            hasChanged = true;
        }
        
        Vector2 directionLooking = new Vector2(
            (float)Math.Cos(_myTank.HullRotation - MathHelper.PiOver2),
            (float)Math.Sin(_myTank.HullRotation - MathHelper.PiOver2)
        );
        
        Vector2 velocity = Vector2.Zero;
        
        if (kState.IsKeyDown(Keys.W))
        {
            velocity += directionLooking * GameConstants.TankSpeed;
            hasChanged = true;
        }
        
        if (kState.IsKeyDown(Keys.S))
        {
            velocity -= directionLooking * GameConstants.TankSpeed * 0.5f;
            hasChanged = true;
        }
        
        // Apply recoil before clamping position
        if (_recoilVelocity.Length() > 0.1f)
        {
            velocity += _recoilVelocity;
            _recoilVelocity *= GameConstants.RecoilDamping;

            hasChanged = true;
        }
        else
        {
            _recoilVelocity = Vector2.Zero;
        }
        
        float hitBoxRadius = GameConstants.TankRadius * 0.6f;

        if (!IsCollidingWithWall(new Vector2(_myTank.X + velocity.X, _myTank.Y), hitBoxRadius))
        {
            _myTank.X += velocity.X;
        }

        if (!IsCollidingWithWall(new Vector2(_myTank.X, _myTank.Y + velocity.Y), hitBoxRadius))
        {
            _myTank.Y += velocity.Y;
        }
        
        _myTank.X = MathHelper.Clamp(_myTank.X, 0, MapWidth);
        _myTank.Y = MathHelper.Clamp(_myTank.Y, 0, MapHeight);
        
        Matrix inverseCamera = Matrix.Invert(_cameraMatrix);
        
        Vector2 mousePosition = new Vector2(mState.X, mState.Y);
        Vector2 worldMousePosition = Vector2.Transform(mousePosition, inverseCamera);
        
        Vector2 tankPosition = new Vector2(_myTank.X, _myTank.Y);
        Vector2 direction = worldMousePosition - tankPosition;
        
        float newTurretRotation = (float)Math.Atan2(direction.Y, direction.X) + MathHelper.PiOver2;
        
        float turretRotationSpeed = MathHelper.ToRadians(GameConstants.TurretRotationSpeedDegrees);
        float angleDiff = MathHelper.WrapAngle(newTurretRotation - _myTank.TurretRotation);
        float maxStep = turretRotationSpeed * (float)gameTime.ElapsedGameTime.TotalSeconds;
        float previousTurretRotation = _myTank.TurretRotation;

        if (Math.Abs(angleDiff) <= maxStep)
        {
            _myTank.TurretRotation = newTurretRotation;
        }
        else
        {
            _myTank.TurretRotation += Math.Sign(angleDiff) * maxStep;
        }
        
        if (Math.Abs(_myTank.TurretRotation - previousTurretRotation) > 0.0001f)
        {
            hasChanged = true;
        }
        
        if (_myTank.Health <= 0)
        {
            _myTank.Health = TankState.MaxHealth;
            _myTank.X = _random.Next(GameConstants.RespawnMinX, GameConstants.RespawnMaxX);
            _myTank.Y = _random.Next(GameConstants.RespawnMinY, GameConstants.RespawnMaxY);
            hasChanged = true;
        }
        
        _netSendTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        float rotThreshold = MathHelper.ToRadians(GameConstants.RotationThresholdDegrees);

        bool turretMoved = Math.Abs(_myTank.TurretRotation - _lastSentTurretRotation) > rotThreshold;
        bool hullMoved = Math.Abs(_myTank.HullRotation - _lastSentHullRotation) > rotThreshold;
        bool posMoved = Vector2.Distance(new Vector2(_myTank.X, _myTank.Y), _lastSentPosition) > GameConstants.PositionThreshold;
        
        for (int i = _explosions.Count - 1; i >= 0; i--)
        {
            _explosions[i].Update(gameTime, _explosionAnimation);
            if (_explosions[i].Finished)
            {
                _explosions.RemoveAt(i);
            }
        }

        if ((turretMoved || hullMoved || posMoved || hasChanged) && _netSendTimer >= NetSendInterval)
        {
            byte[] data = _myTank.ToBytes();
            _networkClient.Send(data, data.Length);

            _lastSentTurretRotation = _myTank.TurretRotation;
            _lastSentHullRotation = _myTank.HullRotation;
            _lastSentPosition = new Vector2(_myTank.X, _myTank.Y);
            _netSendTimer = 0f;
        }
        
        // Update kill feed timers
        for (int i = _killFeed.Count - 1; i >= 0; i--)
        {
            _killFeed[i].Timer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_killFeed[i].Timer <= 0) _killFeed.RemoveAt(i);
        }
        
        base.Update(gameTime);
    }
    
    private void UpdateCamera()
    {
        var screenCenter = new Vector2(_graphics.PreferredBackBufferWidth / 2f, _graphics.PreferredBackBufferHeight / 2f);
        
        Vector2 shakeOffset = Vector2.Zero;
        if (_shakeIntensity > 0.1f)
        {
            shakeOffset = new Vector2(
                (float)(_random.NextDouble() * 2 - 1) * _shakeIntensity,
                (float)(_random.NextDouble() * 2 - 1) * _shakeIntensity
            );

            _shakeIntensity *= GameConstants.ShakeDamping;
        }
        else
        {
            _shakeIntensity = 0f;
        }
        
        // _cameraMatrix = Matrix.CreateTranslation(-_myTank.X + shakeOffset.X, -_myTank.Y + shakeOffset.Y, 0) * Matrix.CreateTranslation(screenCenter.X, screenCenter.Y, 0);
        _cameraMatrix = 
            Matrix.CreateTranslation(-_myTank.X + shakeOffset.X, -_myTank.Y + shakeOffset.Y, 0) * 
            Matrix.CreateScale(_cameraZoom, _cameraZoom, 1f) *
            Matrix.CreateTranslation(screenCenter.X, screenCenter.Y, 0);
    }
    
    private void LoadMap(string filePath)
    {
        if (!System.IO.File.Exists(filePath))
        {
            Console.WriteLine("BŁĄD: Nie znaleziono pliku mapy!");
            return;
        }
        _obstacles.Clear();
        _mapLines = System.IO.File.ReadAllLines(filePath).ToList();

        for (int y = 0; y < _mapLines.Count; y++)
        {
            for (int x = 0; x < _mapLines[y].Length; x++)
            {
                if (_mapLines[y][x] == '#')
                {
                    _obstacles.Add(new Rectangle(x * _tileSize, y * _tileSize, _tileSize, _tileSize));
                }
            }
        }
        
        if (_mapLines.Count > 0)
        {
            MapWidth = _mapLines[0].Length * _tileSize;
            MapHeight = _mapLines.Count * _tileSize;
        }
    }
    
    private bool IsCollidingWithWall(Vector2 position, float collisionRadius)
    {
        int radius = (int)collisionRadius;
        Rectangle boundingBox = new Rectangle(
            (int)position.X - radius, 
            (int)position.Y - radius, 
            radius * 2, 
            radius * 2
        );

        foreach (var obstacle in _obstacles)
        {
            if (boundingBox.Intersects(obstacle))
            {
                return true;
            }
        }
        return false;
    }
    private void DrawTank(TankState tank, float reloadProgress)
    {
    Vector2 tankPosition = new Vector2(tank.X, tank.Y);

    Vector2 hullOrigin = new Vector2(_hullTexture.Width / 2f, _hullTexture.Height * 0.65f);
    Vector2 turretOrigin = new Vector2(_turretTexture.Width / 2f, _turretTexture.Height * 0.75f);
    Vector2 trackOrigin = new Vector2(_tracksTexture.Width / 2f, _tracksTexture.Height / 2f);

    float lateralOffset = 75f;
    float longitudinalOffset = -35f;
    Vector2 leftTrackOffsetLocal = new Vector2(-lateralOffset, longitudinalOffset);
    Vector2 rightTrackOffsetLocal = new Vector2(lateralOffset, longitudinalOffset);

    Matrix rotationMatrix = Matrix.CreateRotationZ(tank.HullRotation);

    Vector2 rotatedLeftOffset = Vector2.Transform(leftTrackOffsetLocal, rotationMatrix);
    Vector2 rotatedRightOffset = Vector2.Transform(rightTrackOffsetLocal, rotationMatrix);
    
    
    // tracks
    _spriteBatch.Draw(_tracksTexture, tankPosition + rotatedLeftOffset, null, Color.White, 
        tank.HullRotation, trackOrigin, 1f, SpriteEffects.None, 0f);
    
    _spriteBatch.Draw(_tracksTexture, tankPosition + rotatedRightOffset, null, Color.White, 
        tank.HullRotation, trackOrigin, 1f, SpriteEffects.None, 0f);
    // hull
    _spriteBatch.Draw(_hullTexture, tankPosition, null, Color.White, 
        tank.HullRotation, hullOrigin, 1.0f, SpriteEffects.None, 0f);
    // turret
    _spriteBatch.Draw(_turretTexture, tankPosition, null, Color.White, 
        tank.TurretRotation, turretOrigin, 1.0f, SpriteEffects.None, 0f);
    // nick
    Vector2 nameSize = _scoreFont.MeasureString(tank.Name);
    Vector2 namePos = new Vector2(tank.X - nameSize.X / 2, tank.Y - 150);
    _spriteBatch.DrawString(_scoreFont, tank.Name, namePos, Color.White);
    // hp
    Vector2 barPos = new Vector2(tank.X - 50, tank.Y - 120);
    int barWidth = 100;
    int currentHpWidth = (int)(barWidth * (tank.Health / (float)TankState.MaxHealth));

    _spriteBatch.Draw(_pixel, new Rectangle((int)barPos.X, (int)barPos.Y, barWidth, 10), Color.Red);
    _spriteBatch.Draw(_pixel, new Rectangle((int)barPos.X, (int)barPos.Y, currentHpWidth, 10), Color.Green);
    
    // reload
    if (reloadProgress < 1.0f)
    {
        int reloadBarHeight = 5;
        int reloadY = (int)barPos.Y + 12;
        int currentReloadWidth = (int)(barWidth * reloadProgress);

        _spriteBatch.Draw(_pixel, new Rectangle((int)barPos.X, reloadY, barWidth, reloadBarHeight), Color.Black * 0.5f);
        _spriteBatch.Draw(_pixel, new Rectangle((int)barPos.X, reloadY, currentReloadWidth, reloadBarHeight), Color.Yellow);
    }
}

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Brown);
        
        if (_isEnteringNick)
        {
            GraphicsDevice.Clear(Color.Black);
            _spriteBatch.Begin();
            string prompt = "ENTER YOUR NICKNAME:";
            Vector2 promptSize = _scoreFont.MeasureString(prompt);
            Vector2 nickSize = _scoreFont.MeasureString(_nickBuffer + "_");
            Vector2 center = new Vector2(GraphicsDevice.Viewport.Width / 2, GraphicsDevice.Viewport.Height / 2);

            _spriteBatch.DrawString(_scoreFont, prompt, center - new Vector2(promptSize.X / 2, 50), Color.White);
            _spriteBatch.DrawString(_scoreFont, _nickBuffer + "_", center - new Vector2(nickSize.X / 2, -20), Color.Yellow);
        
            _spriteBatch.End();
            return;
        }
        
        _spriteBatch.Begin(transformMatrix: _cameraMatrix, samplerState: SamplerState.LinearWrap);       
        
        // for (int x = 0; x < MapWidth; x += _groundTexture.Width)
        // {
        //     for (int y = 0; y < MapHeight; y += _groundTexture.Height)
        //     {
        //         _spriteBatch.Draw(_groundTexture, new Vector2(x, y), Color.White);
        //     }
        // }
        
        for (int y = 0; y < _mapLines.Count; y++)
        {
            for (int x = 0; x < _mapLines[y].Length; x++)
            {
                Vector2 pos = new Vector2(x * _tileSize, y * _tileSize);
        
                _spriteBatch.Draw(_groundTexture, new Rectangle((int)pos.X, (int)pos.Y, _tileSize, _tileSize), Color.White);

                if (_mapLines[y][x] == '#')
                {
                    _spriteBatch.Draw(_wallTexture, new Rectangle((int)pos.X, (int)pos.Y, _tileSize, _tileSize), Color.White);
                }
            }
        }
        
        Vector2 bulletOrigin = new Vector2(_bulletTexture.Width / 2f, _bulletTexture.Height / 2f);
        foreach (var b in _bullets)
        {
            _spriteBatch.Draw(_bulletTexture, b.Position, null, Color.White, b.Rotation, bulletOrigin, 1f, SpriteEffects.None, 0f);
        }

        float reloadProgress = (ShootDelay - _shootTimer) / ShootDelay;
        DrawTank(_myTank, reloadProgress);        
        foreach (var tank in _otherTanks.Values)
        {
            DrawTank(tank, 1.0f);
        }
        
        foreach (var ex in _explosions)
        {
            Rectangle sourceRect = _explosionAnimation.Frames[ex.CurrentFrame];
            Vector2 origin = new Vector2(sourceRect.Width / 2f, sourceRect.Height / 2f);
        
            _spriteBatch.Draw(_explosionSheet, ex.Position, sourceRect, Color.White, 0f, origin, 1.5f, SpriteEffects.None, 0f);
        }
        
        _spriteBatch.End();
        
        _spriteBatch.Begin();
        
        int feedY = 20;
        foreach (var msg in _killFeed)
        {
            _spriteBatch.DrawString(_scoreFont, msg.Text, new Vector2(1602, feedY + 2), Color.Black * 0.5f);
            _spriteBatch.DrawString(_scoreFont, msg.Text, new Vector2(1600, feedY), msg.Color);
            feedY += 30;
        }
        
        _spriteBatch.DrawString(_scoreFont, $"Kills: {_myTank.Kills}", new Vector2(20, 20), Color.Yellow);

        int offset = 50;
        _spriteBatch.DrawString(_scoreFont, "Leaderboard", new Vector2(20, offset), Color.White);
        foreach (var other in _otherTanks.Values)
        {
            offset += 25;
            _spriteBatch.DrawString(_scoreFont, $"{other.Name}: {other.Kills} kills", new Vector2(20, offset), Color.LightGray);
        }
        _spriteBatch.End();
        
        base.Draw(gameTime);
    }
}