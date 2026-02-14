# Tank Mayhem

**Tank Mayhem** is a multiplayer 2D tank shooter built with C# and the MonoGame framework. It utilizes a custom UDP-based client-server architecture to handle real-time multiplayer combat.

## Features
* **Multiplayer Combat:** Real-time action powered by a UDP server that broadcasts tank positions, bullets, and damage packets to all connected players.
* **Project Structure:** Separated into `Client` (rendering, input, audio), `Server` (connection handling and state broadcasting), and `Shared` (network packet definitions).
* **Dynamic Gameplay:** Features WASD movement, mouse-based aiming, recoil physics, camera shake on impact, and animated explosions.
* **Competitive UI:** Includes custom player nicknames, health/reload bars, a live leaderboard, and a real-time kill feed.
* **Custom Maps:** Dynamically loads map layouts and solid obstacles from a simple `map.txt` file.

## Usage
1. **Host the Game:** Run the `Server` project. It will immediately start listening for UDP connections on port 12345.
2. **Launch the Game:** Run the `Client` project.
3. **Connect:** When prompted in the console, enter the server's IP address (or press Enter to default to `127.0.0.1` for local play).
4. **Play:** Type in your nickname in the game window and press Enter. Use **WASD** to move and the **Left Mouse Button** to shoot.