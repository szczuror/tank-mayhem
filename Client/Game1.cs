using System;
using System.Collections.Generic;
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
    private const float NetSendInterval = 0.005f;
    private float _lastSentHullRotation = 0f;
    private float _shootTimer = 0f;
    private const float ShootDelay = 3.0f;
    private const float RecoilStrength = 150f;
    private Vector2 _recoilVelocity = Vector2.Zero;
    private const float RecoilPower = 12f;
    private const float RecoilDamping = 0.85f;
    private Texture2D _pixel;
    private SpriteFont _scoreFont;
    private Texture2D _explosionSheet;
    private Animation _explosionAnimation;
    private List<Explosion> _explosions = new List<Explosion>();
    private const int MapWidth = 3072;
    private const int MapHeight = 3072;
    private Texture2D _groundTexture;
    private SoundEffect _shotSound;
    private Matrix _cameraMatrix;
    private float _shakeIntensity = 0f;
    private Random _random = new Random();
    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth = 1920;
        _graphics.PreferredBackBufferHeight = 1080;
        _graphics.ApplyChanges();
        
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        _networkClient = new UdpClient();
        _networkClient.Connect("127.0.0.1", 12345);

        _myTank = new TankState { Id = (byte)new Random().Next(1, 255), X = 100, Y = 100};
        _lastSentTurretRotation = _myTank.TurretRotation;

        byte[] data = _myTank.ToBytes();
        _networkClient.Send(data, data.Length);

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
                    _otherTanks[incomingTank.Id] = incomingTank;
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

                    if (incomingDmg.TargetId == _myTank.Id)
                    {
                        _myTank.Health -= incomingDmg.DamageAmount;
                    }
                    else if (_otherTanks.ContainsKey(incomingDmg.TargetId))
                    {
                        _otherTanks[incomingDmg.TargetId].Health -= incomingDmg.DamageAmount;
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

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _hullTexture = Content.Load<Texture2D>("assets/PNG/Hulls_Color_A/Hull_01.png");
        _turretTexture = Content.Load<Texture2D>("assets/PNG/Weapon_Color_A/Gun_01.png");
        _tracksTexture = Content.Load<Texture2D>("assets/PNG/Tracks/Track_1_A.png");
        _bulletTexture = Content.Load<Texture2D>("assets/PNG/Effects/Medium_Shell");
        _shotSound = Content.Load<SoundEffect>("assets/AUDIO/shot");
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _scoreFont = Content.Load<SpriteFont>("ScoreFont");
        _explosionSheet = Content.Load<Texture2D>("assets/PNG/Effects/Explosion_merged");
        _explosionAnimation = new Animation(_explosionSheet.Width / 8, _explosionSheet.Height, 8, 75);
        _groundTexture = Content.Load<Texture2D>("assets/Texture/TXTilesetGrass");
    }
    
    

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
            Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();
        
        var kState = Keyboard.GetState();
        var mState = Mouse.GetState();
        float speed = 4.5f;
        float rotationSpeed = 0.025f;
        float turretRotationSpeed = MathHelper.ToRadians(90f);
        bool hasChanged = false;
        float tankRadius = 60f;
        
        UpdateCamera();
        
        if (_shootTimer > 0)
            _shootTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
        
        if (mState.LeftButton == ButtonState.Pressed && _lastMouseState.LeftButton == ButtonState.Released && _shootTimer <= 0)
        {
            _shakeIntensity = 35f;
            float barrelLength = 150f; 
            Vector2 spawnPos = new Vector2(_myTank.X, _myTank.Y) + 
                               new Vector2((float)Math.Cos(_myTank.TurretRotation - MathHelper.PiOver2), 
                                   (float)Math.Sin(_myTank.TurretRotation - MathHelper.PiOver2)) * barrelLength;
            
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
            float randomPitch = (float)(new Random().NextDouble() * 0.2 - 0.1);
            _shotSound.Play(0.5f, randomPitch, 0.0f);
            
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

            if (b.PlayerId == _myTank.Id)
            {
                foreach (var other in _otherTanks.Values)
                {
                    if (Vector2.Distance(b.Position, new Vector2(other.X, other.Y)) < tankRadius)
                    {
                        int damageAmount = 35;
                        if (other.Health <= damageAmount)
                        {
                            _myTank.Kills++;
                        }
                        var dmgPkt = new DamagePacket { 
                            TargetId = other.Id, 
                            DamageAmount = damageAmount
                        };
                        byte[] dmgData = dmgPkt.ToBytes();
                        _networkClient.Send(dmgData, dmgData.Length);
                        
                        _explosions.Add(new Explosion { Position = b.Position });
                        _bullets.RemoveAt(i);
                        bulletRemoved = true;
                        break;
                    }
                }
            }

            if (!bulletRemoved)
            {
                foreach (var other in _otherTanks.Values)
                {
                    if (Vector2.Distance(b.Position, new Vector2(other.X, other.Y)) < tankRadius)
                    {
                        _bullets.RemoveAt(i);
                        bulletRemoved = true;
                        break;
                    }
                }
            }

            if (!bulletRemoved && b.Lifetime <= 0) 
                _bullets.RemoveAt(i);
        }

        if (kState.IsKeyDown(Keys.A)) 
        { 
            _myTank.HullRotation -= rotationSpeed; 
            _myTank.TurretRotation -= rotationSpeed;
            hasChanged = true;
        }
        if (kState.IsKeyDown(Keys.D)) 
        { 
            _myTank.HullRotation += rotationSpeed; 
            _myTank.TurretRotation += rotationSpeed;
            hasChanged = true;
        }
        
        Vector2 directionLooking = new Vector2(
            (float)Math.Cos(_myTank.HullRotation - MathHelper.PiOver2),
            (float)Math.Sin(_myTank.HullRotation - MathHelper.PiOver2)
        );
        
        if (kState.IsKeyDown(Keys.W))
        {
            _myTank.X += directionLooking.X * speed;
            _myTank.Y += directionLooking.Y * speed;
            hasChanged = true;
        }
        
        if (kState.IsKeyDown(Keys.S))
        {
            _myTank.X -= directionLooking.X * speed * 0.5f;
            _myTank.Y -= directionLooking.Y * speed * 0.5f;
            hasChanged = true;
        }
        
        _myTank.X = MathHelper.Clamp(_myTank.X, 0, MapWidth);
        _myTank.Y = MathHelper.Clamp(_myTank.Y, 0, MapHeight);
        
        Matrix inverseCamera = Matrix.Invert(_cameraMatrix);
        
        Vector2 mousePosition = new Vector2(mState.X, mState.Y);
        Vector2 worldMousePosition = Vector2.Transform(mousePosition, inverseCamera);
        
        Vector2 tankPosition = new Vector2(_myTank.X, _myTank.Y);
        Vector2 direction = worldMousePosition - tankPosition;
        
        float newTurretRotation = (float)Math.Atan2(direction.Y, direction.X) + MathHelper.PiOver2;
        
        float angleDiff = MathHelper.WrapAngle(newTurretRotation - _myTank.TurretRotation);
        float maxStep = turretRotationSpeed * (float)gameTime.ElapsedGameTime.TotalSeconds;
        float previousTurretRotation = _myTank.TurretRotation;

        // if(Math.Abs(_myTank.TurretRotation - newTurretRotation) > 0.01f)
        // {
        //     _myTank.TurretRotation = newTurretRotation;
        //     hasChanged = true;
        // }
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
        
        if (_recoilVelocity.Length() > 0.1f)
        {
            _myTank.X += _recoilVelocity.X;
            _myTank.Y += _recoilVelocity.Y;

            _recoilVelocity *= RecoilDamping;

            hasChanged = true;
        }
        else
        {
            _recoilVelocity = Vector2.Zero;
        }
        
        if (_myTank.Health <= 0)
        {
            Random rnd = new Random();
            _myTank.Health = 100;
            _myTank.X = rnd.Next(100, 1280);
            _myTank.Y = rnd.Next(100, 720);
            hasChanged = true;
        }
        
        _netSendTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        float rotThreshold = MathHelper.ToRadians(0.5f);
        float posThreshold = 1.0f;

        bool turretMoved = Math.Abs(_myTank.TurretRotation - _lastSentTurretRotation) > rotThreshold;
        bool hullMoved = Math.Abs(_myTank.HullRotation - _lastSentHullRotation) > rotThreshold;
        bool posMoved = Vector2.Distance(new Vector2(_myTank.X, _myTank.Y), _lastSentPosition) > posThreshold;
        
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

            _shakeIntensity *= 0.9f;
        }
        else
        {
            _shakeIntensity = 0f;
        }
        
        _cameraMatrix = Matrix.CreateTranslation(-_myTank.X + shakeOffset.X, -_myTank.Y + shakeOffset.Y, 0) * Matrix.CreateTranslation(screenCenter.X, screenCenter.Y, 0);
    }
    private void DrawTank(TankState tank)
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
    
    Vector2 barPos = new Vector2(tank.X - 50, tank.Y - 120);
    int barWidth = 100;
    int currentHpWidth = (int)(barWidth * (tank.Health / 100f));

    _spriteBatch.Draw(_pixel, new Rectangle((int)barPos.X, (int)barPos.Y, barWidth, 10), Color.Red);
    _spriteBatch.Draw(_pixel, new Rectangle((int)barPos.X, (int)barPos.Y, currentHpWidth, 10), Color.Green);
}

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);
        _spriteBatch.Begin(transformMatrix: _cameraMatrix, samplerState: SamplerState.LinearWrap);       
        
        for (int x = 0; x < MapWidth; x += _groundTexture.Width)
        {
            for (int y = 0; y < MapHeight; y += _groundTexture.Height)
            {
                _spriteBatch.Draw(_groundTexture, new Vector2(x, y), Color.White);
            }
        }
        
        Vector2 bulletOrigin = new Vector2(_bulletTexture.Width / 2f, _bulletTexture.Height / 2f);
        foreach (var b in _bullets)
        {
            _spriteBatch.Draw(_bulletTexture, b.Position, null, Color.White, b.Rotation, bulletOrigin, 1f, SpriteEffects.None, 0f);
        }

        DrawTank(_myTank);        
        foreach (var tank in _otherTanks.Values)
        {
            DrawTank(tank);
        }
        
        foreach (var ex in _explosions)
        {
            Rectangle sourceRect = _explosionAnimation.Frames[ex.CurrentFrame];
            Vector2 origin = new Vector2(sourceRect.Width / 2f, sourceRect.Height / 2f);
        
            _spriteBatch.Draw(_explosionSheet, ex.Position, sourceRect, Color.White, 0f, origin, 1.5f, SpriteEffects.None, 0f);
        }
        
        _spriteBatch.End();
        
        _spriteBatch.Begin();
        _spriteBatch.DrawString(_scoreFont, $"Kills: {_myTank.Kills}", new Vector2(20, 20), Color.Yellow);

        int offset = 50;
        _spriteBatch.DrawString(_scoreFont, "Leaderboard", new Vector2(20, offset), Color.White);
        foreach (var other in _otherTanks.Values)
        {
            offset += 25;
            _spriteBatch.DrawString(_scoreFont, $"Player {other.Id}: {other.Kills} kills", new Vector2(20, offset), Color.LightGray);
        }
        _spriteBatch.End();
        
        base.Draw(gameTime);
    }
}

public class Bullet {
    public byte PlayerId;
    public Vector2 Position;
    public float Rotation;
    public float Speed = 20f;
    public float Lifetime = 2.0f;
}

public class Animation
{
    public List<Rectangle> Frames { get; set; }
    public TimeSpan Delay { get; set; }

    public Animation(int frameWidth, int frameHeight, int frameCount, int msDelay)
    {
        Frames = new List<Rectangle>();
        for (int i = 0; i < frameCount; i++)
        {
            Frames.Add(new Rectangle(i * frameWidth, 0, frameWidth, frameHeight));
        }
        Delay = TimeSpan.FromMilliseconds(msDelay);
    }
}

public class Explosion 
{
    public Vector2 Position;
    public int CurrentFrame = 0;
    public TimeSpan Elapsed = TimeSpan.Zero;
    public bool Finished = false;

    public void Update(GameTime gameTime, Animation animation)
    {
        Elapsed += gameTime.ElapsedGameTime;
        if (Elapsed >= animation.Delay)
        {
            Elapsed -= animation.Delay;
            CurrentFrame++;
            if (CurrentFrame >= animation.Frames.Count)
            {
                Finished = true;
            }
        }
    }
}