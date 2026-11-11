# Tiny Swords Island Kit — tile rules, assets and an island generator recipe

Goal: build islands that look like the official Tiny Swords promo (grass on animated teal water, white foam ring,
stacked plateaus with stone cliffs and soft shadows, stairs, animated trees/bushes/sheep, rocks in the water, clouds),
using **only** the real assets in `Assets/Tiny Swords/` (Pixel Frog, "Tiny Swords" 64 px version with Lancer/Monk units).

Everything below comes from reading the asset `.meta`/`.asset` YAML, the 7 "Unity Tile Guide" images and the
`Demo Terrain-Tiles.unity` scene. The autotiling rules were **checked against every elevated tile in the demo scene
(52/52 cells predicted correctly)**, and a Python prototype that follows section 7 renders a correct island from the real sprites.

---

## 0. TL;DR (key facts)

| Fact | Value |
|---|---|
| Grid | 64 px = 1 world unit (PPU 64 on every Tiny Swords texture), Grid cellSize (1,1,0), TileAnchor (0.5,0.5) |
| Terrain sheets | `Terrain/Tileset/Tilemap_color1..5.png`, 576×384 = 9×6 cells, **44 sprites each, identical layout/rects in all 5 colours** |
| Tile assets | `Terrain/Tileset/Tilemap Settings/Sliced Tiles/Tilemap_color{K}_{i}.asset` (K=1..5, i=0..43), plain `UnityEngine.Tilemaps.Tile`, each references the sprite with the same name (verified 220/220) |
| Extra tile | `Sliced Tiles/Tilemap_color1.asset` is a **duplicate** of `Tilemap_color1_1` (top edge). Ignore it. |
| Water | `Tilemap Settings/Water Background color.asset` (Tile, 64×64 flat colour **#47ABA9** = RGB 71,171,169) |
| Foam | `Tilemap Settings/Water Tile animated.asset` — **despite the name this is the FOAM**: `AnimatedTile` (tilemap.extras), 16 frames of `Water Foam.png` (192×192 each), speed random 9–11 fps per cell |
| Shadow | `Tilemap Settings/Shadow.asset` (Tile → `Shadow.png` 192×192 single sprite; opaque blob 79×80 px, colour RGB 22,28,46 at alpha 80/255) |
| Layer order | BG 0 → Foam 1 → Flat 2 → (Shadow, Elevated) per level: 3/4, 5/6, 7/8 … |
| Cliff | exactly **one row** directly under the bottom edge of every elevated plateau |
| Autotile | pure **4-neighbour mask** (16 pieces, **no inner-corner tiles**), same for flat and elevated tops |
| Shadow | under every walkable elevated cell, **shifted 1 cell down** |
| Foam | under every land cell that touches water (flat ground and water-facing cliffs) |
| Sorting layers in project | only `Default` (ProjectSettings/TagManager.asset). Use order-in-layer bands. |
| Animations | **257/258 Tiny Swords `.anim` clips have `m_LoopTime: 0`** (play once and freeze). Only `Units/Extra/Heal Effect/Heal_Animation.anim` loops. All clips are 10 fps. Controllers have no parameters and no transitions. |
| Pivots | every sprite is centre-pivot (alignment 0, 0.5/0.5). For Y-sorting, use feet offsets (section 8.3). |

Paths below are relative to `Assets/Tiny Swords/` unless they start with `Assets/`.

---

## 1. The 7 official tile-guide images (`Terrain/Tileset/Unity Tile Guide/`)

The text is white on transparent; composite it onto a dark background to read it.

| Image | What it says |
|---|---|
| `_01` Tilemap Parts | Flat Ground = lowest terrain next to water. Elevated Ground = raised terrain with **two cliff types: one facing the terrain below, one facing the water**. Stairs connect a lower terrain to the upper one. Water Foam = waves where any terrain touches the water. Shadow adds depth to each elevated layer above. BG Color fills all extra space. Tile size 64×64. |
| `_02` Tilemap Layers | Order: (0) BG Color, (1) Water Foam, (2) Flat Ground, then **Shadow + Elevated Ground repeated per elevation level**: (3) Shadow, (4) Elevated, (5) Shadow, (6) Elevated, (7) Shadow, (8) Elevated. Use different terrain colours per level so levels read clearly. |
| `_03` Shadow | Shadow sprite (the guide says 128×128, but the shipped PNG is a 192×192 canvas) is bigger than a tile but is placed on the 64×64 grid like any tile, so the edges overlap. It sits under the same area as the **walkable part** of the elevated ground, **moved one full tile (64 px) down**. |
| `_04` Water Foam | Animated sprite with the same setup as Shadow (oversized, placed on the 64 grid). Placed in **every area that touches the BG colour (water)**. Start the foam tiles on different frames so it looks natural. |
| `_05` Flat Ground | Numbered sheet 1–16 plus an example using every piece (see section 3). |
| `_06` Elevated Ground | The top works **exactly like Flat Ground** (pieces 1–16). Cliffs: 17–20 connect to walkable terrain below, 21–24 connect to water. |
| `_07` Stairs | Stairs connect to any elevated area. Next to a walkable area (Example 1), the elevated tile beside the stair top is a **centre-type piece** (no edge on the stair side). Next to a cliff (Example 2), use two pieces: one joins the walkable top, the other joins the **bottom part of the cliff** (cliff piece without an edge on the stair side). |

---

## 2. Tileset anatomy (`Terrain/Tileset/Tilemap_color{K}.png`)

Import settings (all 5 sheets): Sprite Mode Multiple, PPU 64, Filter Point, no mipmaps, Sprite Mesh Type Tight,
Extrude 1, every slice 64×64 with centre pivot. Sheet column 4 is empty.

```
            c0      c1      c2      c3   |c4|   c5      c6      c7      c8
 row0      F1  0   F2  1   F3  2   F13 3  |  |  E1  4   E2  5   E3  6   E13 7
 row1      F4  8   F5  9   F6 10   F14 11 |  |  E4 12   E5 13   E6 14   E14 15
 row2      F7 16   F8 17   F9 18   F15 19 |  |  E7 20   E8 21   E9 22   E15 23
 row3      F10 24  F11 25  F12 26  F16 27 |  |  E10 28  E11 29  E12 30  E16 31
 row4      SL^ 32   .       .      SR^ 33 |  |  C17 34  C18 35  C19 36  C20 37
 row5      SLv 38   .       .      SRv 39 |  |  C21 40  C22 41  C23 42  C24 43
```
`Fn` = flat guide number, `En` = elevated-top guide number, `Cn` = cliff guide number, trailing number = sprite index
(`Tilemap_colorK_<index>`). `SL` = stair on the LEFT side of a plateau (ramp rises to the right, plateau is East of it),
`SR` = stair on the RIGHT side of a plateau (ramp rises to the left, plateau is West of it). `^` = top piece, `v` = bottom piece.

Visual notes:
- Flat tiles have a light, wet-looking rim baked into their outer edges. Elevated tops have only the dark outline.
- Cliff row 4 (C17–20) ends in a dark outline and needs ground drawn underneath. Cliff row 5 (C21–24) has foam baked into its bottom.
- Flat centre (9) and elevated centre (13) are pixel-identical (the demo uses them interchangeably).

### 2.1 Full slice table (identical for colours 1–5; internal IDs listed for colour 1)

Unity rect origin is bottom-left (y=320 is the top row). Internal ID = `fileID` of the sprite inside the PNG (read from
`nameFileIdTable` in the `.meta`). The IDs for colours 2–5 differ, so load the Tile assets by **path/name**, never by ID.

| idx | sheet cell | rect x,y (64×64) | guide | role | color1 internalID |
|---:|---|---|---|---|---|
| 0 | c0 r0 | 0,320 | F1 | flat corner top-left | 4684625920699962428 |
| 1 | c1 r0 | 64,320 | F2 | flat top edge | -2801873246907429078 |
| 2 | c2 r0 | 128,320 | F3 | flat corner top-right | -1844109271 |
| 3 | c3 r0 | 192,320 | F13 | flat 1-wide column, top end | -1483446518 |
| 4 | c5 r0 | 320,320 | E1 | elev corner top-left | -357285377 |
| 5 | c6 r0 | 384,320 | E2 | elev top edge | 1908527803 |
| 6 | c7 r0 | 448,320 | E3 | elev corner top-right | 175773559 |
| 7 | c8 r0 | 512,320 | E13 | elev 1-wide column, top end | -1243141261 |
| 8 | c0 r1 | 0,256 | F4 | flat left edge | -529755566 |
| 9 | c1 r1 | 64,256 | F5 | flat centre | -985832654 |
| 10 | c2 r1 | 128,256 | F6 | flat right edge | 1273371556 |
| 11 | c3 r1 | 192,256 | F14 | flat 1-wide column, middle | -438687076 |
| 12 | c5 r1 | 320,256 | E4 | elev left edge | -1511874181 |
| 13 | c6 r1 | 384,256 | E5 | elev centre | 1317816526 |
| 14 | c7 r1 | 448,256 | E6 | elev right edge | 849504406 |
| 15 | c8 r1 | 512,256 | E14 | elev 1-wide column, middle | -510447500 |
| 16 | c0 r2 | 0,192 | F7 | flat corner bottom-left | 1611736068 |
| 17 | c1 r2 | 64,192 | F8 | flat bottom edge | -160155248 |
| 18 | c2 r2 | 128,192 | F9 | flat corner bottom-right | 659350005 |
| 19 | c3 r2 | 192,192 | F15 | flat 1-wide column, bottom end | 196836861 |
| 20 | c5 r2 | 320,192 | E7 | elev corner bottom-left | -1675646848 |
| 21 | c6 r2 | 384,192 | E8 | elev bottom edge | -916154361 |
| 22 | c7 r2 | 448,192 | E9 | elev corner bottom-right | 543361271 |
| 23 | c8 r2 | 512,192 | E15 | elev 1-wide column, bottom end | -2041690308 |
| 24 | c0 r3 | 0,128 | F10 | flat 1-tall row, left end | 1599712627 |
| 25 | c1 r3 | 64,128 | F11 | flat 1-tall row, middle | 1292135869 |
| 26 | c2 r3 | 128,128 | F12 | flat 1-tall row, right end | -55024715 |
| 27 | c3 r3 | 192,128 | F16 | flat isolated 1×1 | 157780901 |
| 28 | c5 r3 | 320,128 | E10 | elev 1-tall row, left end | 1881934651 |
| 29 | c6 r3 | 384,128 | E11 | elev 1-tall row, middle | 249465112 |
| 30 | c7 r3 | 448,128 | E12 | elev 1-tall row, right end | -1266434703 |
| 31 | c8 r3 | 512,128 | E16 | elev isolated 1×1 | 1127109569 |
| 32 | c0 r4 | 0,64 | — | stair SL top | 1701980172 |
| 33 | c3 r4 | 192,64 | — | stair SR top | -148372578 |
| 34 | c5 r4 | 320,64 | C17 | cliff→ground, left end | -1080117708 |
| 35 | c6 r4 | 384,64 | C18 | cliff→ground, middle | -1635458254 |
| 36 | c7 r4 | 448,64 | C19 | cliff→ground, right end | -1400673839 |
| 37 | c8 r4 | 512,64 | C20 | cliff→ground, single | 1065079252 |
| 38 | c0 r5 | 0,0 | — | stair SL bottom | 1856551384 |
| 39 | c3 r5 | 192,0 | — | stair SR bottom | -1599487688 |
| 40 | c5 r5 | 320,0 | C21 | cliff→water, left end | 1160597804 |
| 41 | c6 r5 | 384,0 | C22 | cliff→water, middle | 565056302 |
| 42 | c7 r5 | 448,0 | C23 | cliff→water, right end | 227355346 |
| 43 | c8 r5 | 512,0 | C24 | cliff→water, single | 137047993 |

### 2.2 Colour variants (grass sample RGB at flat centre / elevated top)

| Sheet | Flat grass | Elev grass | Look | Demo usage |
|---|---|---|---|---|
| `Tilemap_color1` | 184,185,88 | 128,172,94 | yellow-green | Level 1 (Layer 4) |
| `Tilemap_color2` | 147,186,79 | 120,158,95 | fresh green | Flat ground (Layer 2) |
| `Tilemap_color3` | 116,179,99 | 93,160,103 | cool green | Level 2 (Layer 6) |
| `Tilemap_color4` | 150,159,100 | 119,145,92 | olive | Level 3 (Layer 8) |
| `Tilemap_color5` | 99,164,134 | 82,142,137 | teal-green | one 1-wide flat strip |

Cliff stone is identical in all colours (RGB 102,157,157). Recommended default: **flat = color2, L1 = color1, L2 = color3, L3 = color4** (same as the demo).

### 2.3 Special tiles and textures

| Asset | Type | Sprite | Notes |
|---|---|---|---|
| `Terrain/Tileset/Tilemap Settings/Water Background color.asset` | Tile | `Water Background color.png` 64×64 single | solid #47ABA9; m_Flags 1 (LockColor), ColliderType Sprite |
| `Terrain/Tileset/Tilemap Settings/Water Tile animated.asset` | **AnimatedTile** (`Unity.2D.Tilemap.Extras`) | `Water Foam.png` 3072×192, 16 slices `Water Foam_0..15`, 192×192, centre pivot | MinSpeed 9, MaxSpeed 11, StartTime 0, StartFrame 0, no collider. Each frame's opaque area is about 86×90 px, so it spills about 6–12 px left/right/up and up to about 21 px down past the 64 px cell. (The meta's internalIDToNameTable lists more `Water Foam_N` names than the 16 actual slices: these are leftovers.) |
| `Terrain/Tileset/Tilemap Settings/Shadow.asset` | Tile | `Shadow.png` 192×192 **single** sprite, centre pivot | opaque rect x57–136, y56–136 (about 8 px spill on each side of the cell); stale extra slice entries in the meta are unused (sprite mode Single) |
| `Terrain/Tileset/Tilemap Settings/Tilemap.prefab` | **Tile Palette** (Grid + child `Layer1` Tilemap + `Palette Settings`) | — | Not a layer template. Only useful for hand painting in the Tile Palette window. |

Guids (for YAML-level work): Water Tile animated `8f3054a4926764fb58dbabffc35a76b8`, Shadow.asset
`321f95fbeb8ae405a90113d0f2a1c35f`, Water Background color.asset `ab40de2f0156a4e208fad1347f35016b`,
Tilemap_color1.png `027fd9e06196b4e62a8e40527bfbf3a9`, Water Foam.png `473188d9507f64599a776617a45dcf3f`,
Shadow.png `0d08322cf406f4512bdf8ddfaac15258`.

---

## 3. The demo scene (`Scenes/Demo Terrain-Tiles.unity`)

One `Grid` ("Tilemap") with child Tilemaps. All are at local position (0,0,0) with no offsets, TileAnchor (0.5,0.5,0),
TilemapRenderer **Mode = Chunk** (0), Detect Chunk Culling Bounds = Auto, material Sprites-Default, white colour,
`Tilemap.animationFrameRate = 1`.

| GameObject | sortingOrder | sortingLayerID in YAML | Tiles | Content |
|---|---:|---|---:|---|
| Layer 0 - BG Color | 0 | -167479465 | 130 | `Water Background color` filling x 4..16, y -11..-2 |
| Layer 1 - Water Foam | 1 | -1997108105 | 19 | `Water Tile animated` (hand-placed, only where visible) |
| Layer 2 - Flat Ground | 2 | -249056393 | 34 | color2 (+3 color5 cells) |
| Layer 3 - Shadow | 3 | -1809649563 | 11 | `Shadow` |
| Layer 4 - Elevated Ground | 4 | -1273922603 | 34 | color1 (level 1) |
| Layer 5 - Shadow | 5 | 1018183361 | 9 | `Shadow` |
| Layer 6 - Elevated Ground | 6 | -1360470059 | 14 | color3 (level 2) |
| Layer 7 - Shadow | 7 | -754820905 | 2 | `Shadow` |
| Layer 8 - Elevated Ground | 8 | -1228978115 | 4 | color4 (level 3) |
| All Tiles (example) | 0 | -167479465 | 223 | all 5 colour sheets laid out, plus one BG/Shadow/Foam sample at x -18/-16/-14 |

The sortingLayerIDs come from the asset author's project. They **do not exist in this project** (TagManager only has
`Default`, id 0), so Unity falls back to Default. Ordering in practice is **sortingOrder 0..8**. The demo's foam and
shadow cells are a hand-picked subset (only where they are visible). A generator can place them more fully (section 6).

Demo elevated layers (x→, y↓; labels as in section 2; `.` = empty):
```
Layer 4 (L1, color1)      x: 6    7    8    9   10   11   12   13   14
 y -5                       E1   E2   E2   E2   E2   E2   E2  E11   E3
 y -6                       E7   F5   F5   F5   F5   F5   E9  C18  E14
 y -7                      C21   E7   E8   E8   E8   E9  C19  SL^   E9
 y -8                        .  C21  C22  C22  C22  C19    .  SLv  C19
Layer 6 (L2, color3)      x: 7    8    9   10   11
 y -4                       E1   E2   E2   E2  SR^
 y -5                       E7   E8   E8   E9  SRv
 y -6                      C17  C18  C18  C19    .
Layer 8 (L3, color4)      x: 8    9
 y -3                      E10  SR^
 y -4                      C17  SRv
```
Flat (Layer 2) under them, e.g. row y -9: `F16 . . F10 F11 F8 F8 F8 F8 F9` (x 6..15). Under the water cliffs C21/C22
(x 6..10, y -7/-8) there is **no** flat tile. Under the ground cliffs C19 (x 11/12/14) there **is** flat ground.

---

## 4. Derived autotiling rules (verified)

Notation: `N,E,S,W` = cell above/right/below/left on screen (Unity +y is up). Mask bits: **N=1, E=2, S=4, W=8**.

### 4.1 Flat ground (level 0) and elevated tops (level ≥ 1): the same 16-piece 4-neighbour mask

A neighbour "counts" if it holds a top/grass tile **of the same layer** (cliff and stair tiles do not count, except for the stair rule in 4.3).
Diagonals are ignored and there are no inner-corner pieces: a concave corner simply uses the centre/edge piece.

| mask | neighbours present | piece | Flat idx | Elevated idx |
|---:|---|---|---:|---:|
| 0 | none | isolated 1×1 (F16/E16) | 27 | 31 |
| 1 | N | column bottom end (15) | 19 | 23 |
| 2 | E | row left end (10) | 24 | 28 |
| 3 | N E | corner bottom-left (7) | 16 | 20 |
| 4 | S | column top end (13) | 3 | 7 |
| 5 | N S | column middle (14) | 11 | 15 |
| 6 | E S | corner top-left (1) | 0 | 4 |
| 7 | N E S | left edge (4) | 8 | 12 |
| 8 | W | row right end (12) | 26 | 30 |
| 9 | N W | corner bottom-right (9) | 18 | 22 |
| 10 | E W | row middle (11) | 25 | 29 |
| 11 | N E W | bottom edge (8) | 17 | 21 |
| 12 | S W | corner top-right (3) | 2 | 6 |
| 13 | N S W | right edge (6) | 10 | 14 |
| 14 | E S W | top edge (2) | 1 | 5 |
| 15 | N E S W | centre (5) | 9 | 13 |

```csharp
static readonly int[] FlatByMask = { 27,19,24,16, 3,11, 0, 8,26,18,25,17, 2,10, 1, 9 };
static readonly int[] ElevByMask = { 31,23,28,20, 7,15, 4,12,30,22,29,21, 6,14, 5,13 }; // = Flat + 4
```
Check: demo 30/34 flat cells match. The 4 that don't are flat cells completely hidden under a level-1 plateau, where the artist
used a centre tile. Any choice is invisible there.

### 4.2 Cliffs (one row, part of the elevated layer's tilemap)

- **Where:** for every level-L top cell `c` whose South neighbour is not level-L top, put a cliff tile at `c+S`,
  on the **same tilemap and colour as level L**. Cliffs are always exactly 1 row tall, no matter how much height they represent.
- **Which piece:** look only left/right. `Wopen` = W neighbour is a level-L top **or** level-L cliff (or the bottom of an SL stair, see 4.3).
  `Eopen` likewise (or the bottom of an SR stair).

| Wopen | Eopen | piece | ground-facing idx | water-facing idx |
|:---:|:---:|---|---:|---:|
| 0 | 0 | single (C20 / C24) | 37 | 43 |
| 1 | 0 | right end (C19 / C23) | 36 | 42 |
| 0 | 1 | left end (C17 / C21) | 34 | 40 |
| 1 | 1 | middle (C18 / C22) | 35 | 41 |

```csharp
static readonly int[] CliffGround = { 37, 36, 34, 35 }; // index = (Wopen?1:0) | (Eopen?2:0)
static readonly int[] CliffWater  = { 43, 42, 40, 41 };
```
- **Ground vs water:** if the cliff cell itself has lower land under it (flat or a lower level), use **ground-facing (17–20)**
  and keep that lower land tile drawn under the cliff. If the cliff cell is water, use **water-facing (21–24)**: it has foam baked
  in, there is no flat tile under it, and a foam tile goes under it.
- Note that a plateau cell next to the plateau's own lower grass also counts as "open". Example: demo (12,-7) C19 has the E9 grass
  of the same plateau on its West.

### 4.3 Stairs (2 cells tall, 1 wide, on the elevated layer of the higher level L)

- `SL` (idx **32** top / **38** bottom): the stair stands **West of the plateau**; its ramp rises to the East.
- `SR` (idx **33** top / **39** bottom): the stair stands **East of the plateau**; its ramp rises to the West.
- Top piece at `(x,y)`, bottom piece at `(x,y-1)`, drawn on the level-L tilemap with level-L's colour. The landing cell
  `(x,y-2)` must be walkable level L-1.
- The plateau neighbour of the **top** piece (on its high side) must be a level-L top cell. That cell **treats the stair top as a connected neighbour**
  (no edge on that side). Demo: E10 next to SR^ in Layer 8; E9 to the East of SL^ in Layer 4. This is the guide's "centre piece" rule.
- The plateau neighbour of the **bottom** piece is either a level-L top (Example 1: it does **not** connect and keeps its edge,
  e.g. demo Layer 6 E9 next to SRv) or a level-L cliff (Example 2: the cliff **does** connect, e.g. Layer 8 C17 next to SRv,
  Layer 4 C19 to the East of SLv).
- Neighbours on the stair's low side never connect. Example: demo (12,-7) C19 keeps its right edge next to an SL^ that leans East.
- Under the stair cells, draw level L-1 land: the stair art is transparent above/beside the ramp.
- A stair top counts as "walkable" for the level-L shadow (demo Layer 3 has a shadow under SL^ at (13,-8)).
- Preferred placement (guide 07): beside the plateau's side, top piece next to the lowest grass row, bottom piece next to the cliff row
  (Example 2). Example 1 (both pieces next to grass rows) also works. Avoid putting a stair directly under a level-L top cell
  (the cliff of that cell would land on the stair top). The demo's notch variant puts the cliff one row higher, with grass two rows above the stair top.

### 4.4 Shadow

`Shadow[L]` (sortingOrder 2L+1, between level L-1 and level L) gets the Shadow tile at `c+S` for **every level-L top cell `c`
and every level-L stair-top cell**. Demo: Layer 5 = Layer 6 tops shifted down by one, Layer 7 = Layer 8 top + SR^ shifted down.
Most of it is covered by the plateau itself. The visible result is the dark 8 px spill around the cliff row's sides and bottom, falling on the
level below. Level 0 (flat) has no shadow.

### 4.5 Foam

`Foam` (sortingOrder 1) gets `Water Tile animated` on every "solid" cell (flat land or water-facing cliff cell) that has at
least one water cell among its 8 neighbours. Interior cells are optional (they are invisible). The demo places foam under flat
edge cells **and under water-facing cliff cells** (C21/C22 at y -8).
Desync: AnimatedTile already picks a random speed between 9 and 11 fps per cell (demo cells show e.g. `m_AnimationSpeed: 10.46`).
To also randomise the phase, call `tilemap.SetAnimationFrame(cell, Random.Range(0,16))` after painting (Tilemap API added in 2022.2; confirm it exists in 6000.3),
or make 4 copies of the AnimatedTile asset **in your own folder** with `m_AnimationStartFrame` 0/4/8/12 and pick one at random.

### 4.6 Water background

Fill a rectangle that is clearly bigger than the camera view (portrait mobile: aim for at least ±12 cells horizontally and ±20 vertically
around the island) with `Water Background color.asset` on order 0. Alternative: camera clear colour #47ABA9, plus
a BG tilemap only where needed.

---

## 5. Decorations, resources, FX (frame sizes, clips, controllers)

All clips: 10 fps (`m_SampleRate 10`), **non-looping** (except Heal). All controllers: one or two states, default = the first idle/static state,
no parameters, no transitions (switch with `animator.Play("StateName")`). All sprites: PPU 64, centre pivot.
"Feet" = opaque bottom below sprite centre, in px (= units × 64). Use it for Y-sort and ground placement.

### 5.1 Terrain decorations (`Terrain/Decorations/`)

| Item | Texture (frames × size) | Clip / controller | Feet (px below centre) | Notes |
|---|---|---|---:|---|
| Bush 1–4 | `Bushes/Bush N.png` 8 × 128×128 | `Bushes/Bush N Animation/Bush N Animation.anim` (0.8 s) / `Bush N.controller` | 15–20 | wind sway; on grass, often along cliff bases |
| Rocks in water 1–4 | `Rocks in the Water/Water Rocks_0N.png` 16 × 64×64 | `Rocks in the Water/Rock N Animation/Rock N Animation.anim` (1.6 s) / `Rock N.controller` | 16–20 | own foam ring baked in; place on water 1–3 cells off shore |
| Rocks (land) 1–4 | `Rocks/RockN.png` 64×64 single | — | 19–24 | static |
| Clouds 1–8 | `Clouds/Clouds_01..08.png` 576×256 single (9×4 cells) | — | — | **dark drop-shadow baked in**, so they are meant to float above everything (sky layer); drift slowly |
| Rubber duck | `Rubber Duck/Rubber duck.png` 3 × 32×32 | `Rubber Duck/Rubber duck Animation.anim` (0.3 s) / `Rubber duck.controller` | 12 | water easter egg |

### 5.2 Resources (`Pawn and Resources/`)

| Item | Texture | States → clips | Feet | Notes |
|---|---|---|---:|---|
| Tree 1, 2 | `Wood/Trees/Tree1.png`, `Tree2.png` 8 × 192×256 | `Wood/Trees/Tree N Animation/Tree N.controller`: `Tree N_Idle` (8 f, 0.8 s, default), `Tree N_Chopped` (1 f → `Stump N.png`) | 113 / 121 | big leafy trees (3×4 cells) |
| Tree 3, 4 | `Tree3.png`, `Tree4.png` 8 × 192×192 | same pattern (Tree 4 idle clip is named `Tree 4 _Idle.anim`, with a space) | 74 / 72 | smaller (3×3 cells) |
| Stump 1–4 | `Wood/Trees/Stump N.png` (1–2: 192×256, 3–4: 192×192) single | used by `_Chopped` | 112 / 72 | |
| Sheep | `Meat/Sheep/Sheep_Idle.png` 6 × 128, `Sheep_Move.png` 4 × 128, `Sheep_Grass.png` 12 × 128 | `Meat/Sheep/Sheep Animations/Sheep.controller`: `Sheep_Idle` (default), `Sheep_Run` (→Move), `Sheep_Grass` (1.2 s, grazing) | 20 | groups of 2–4 on flat grass |
| Gold stones 1–6 | `Gold/Gold Stones/Gold Stone N.png` 128×128 + `_Highlight.png` 6 × 128 | `Gold/Gold Stones/Gold Stone N Animation/Gold Stone N.controller`: `_Static` (default), `_Highlight` (0.6 s) | 15 (1) … 34 (6) | sizes grow 1→6 |
| Gold resource | `Gold/Gold Resource/Gold_Resource.png` 128 + `_Highlight` 6 × 128 | `Gold Resource Animations/Gold Resource.controller` | — | pickup |
| Wood / Meat resource | `Wood/Wood Resource/Wood Resource.png`, `Meat/Meat Resource/Meat Resource.png` 64×64 | — | — | pickups / icons |
| Tools 1–4 | `Tools/Tool_01..04.png` 64×64 | — | — | |

### 5.3 Particle FX (`Particle FX/`)

| FX | Texture | Clip / controller |
|---|---|---|
| Dust 1 / 2 | `Dust_01.png` 8 × 64, `Dust_02.png` 10 × 64 | `Dust 1 Animation/Dust 1 Animation.anim` + `Dust 1.controller`; `Dust 2 Animation/…` |
| Explosion 1 / 2 | `Explosion_01.png` 8 × 192, `Explosion_02.png` 10 × 192 | `Explosion N Animation/Explosion N Animation.anim` + `.controller` |
| Fire 1 / 2 / 3 | `Fire_01.png` 8 × 64, `Fire_02.png` 10 × 64, `Fire_03.png` 12 × 64 | `Fire 1 Animation/…`, `Fire 2 Animation/…`, **`FIre 3 Animation/`** (typo in folder name) `Fire 3 Animation.anim` |
| Water splash | `Water Splash.png` 9 × 192 | `Water Splash/Water Splash Animation.anim` (0.9 s) + `Water Splash.controller` |
| Heal (Extra) | `Units/Extra/Heal Effect/Heal_Effect.png` 11 × 192 | `Heal_Animation.anim` (**the only looping clip**) + `Heal.controller` |
| Arrow | `Units/Extra/Arrow/Arrow.png` 64×64 | — |

---

## 6. Buildings and units

### 6.1 Buildings (`Buildings/<Black|Blue|Purple|Red|Yellow> Buildings/*.png`, single sprites, same sizes in every colour)

| Sprite | px | cells (w×h) | opaque bbox (x0,y0,x1,y1) | feet below centre | suggested ground footprint (w×d cells) |
|---|---|---|---|---:|---|
| Castle | 320×256 | 5×4 | 4,41,316,249 | 121 | 5×2 (door centred) |
| Barracks | 192×256 | 3×4 | 4,58,188,245 | 106 | 3×2 |
| Archery | 192×256 | 3×4 | 3,61,186,240 | 104 | 3×2 (fence on left) |
| Monastery | 192×320 | 3×5 | 16,45,176,310 | 141 | 3×2 |
| Tower | 128×256 | 2×4 | 4,46,124,230 | 94 | 2×1 |
| House1/2/3 | 128×192 | 2×3 | ~0..128, 16..178 | 69 / 77 / 70 | 2×1 |

Place the sprite so its feet line sits on the bottom edge of the footprint: `spriteCenterY = footprintBottomY + feetPx/64`.
Footprint cells are blocked for walking. Buildings must stand fully on one level (never across a cliff row).

### 6.2 Units, Red (`Units/Red Units/`). Other colours have the same structure (23 PNG + 4 controllers each).

| Unit | Frame | Sheets (frames) | Controller (states) | Feet |
|---|---|---|---|---:|
| Warrior | 192×192 | `Warrior/Warrior_Idle.png` (8), `_Run` (6), `_Attack1` (4), `_Attack2` (4), `_Guard` (6) | `Warrior/Warrior Red Animations/Warrior.controller`: `Warrior_Idle_Red` (default), `Warrior_Run_Red`, `Warrior_Attack1_Red`, `Warrior_Attack2_Red`, `Warrior_Guard_Red` | 41 |
| Archer | 192×192 | `Archer/Archer_Idle.png` (6), `Archer_Run.png` (4), `Archer_Shoot.png` (8) | `Archer/Archer Animations Red/Archer_Red.controller`: `Archer_Idle_Red`, `Archer_Run_Red`, `Archer_Shoot_Red` | 40 |
| Monk | 192×192 | `Monk/Idle.png` (6), `Monk/Run.png` (4), `Monk/Heal.png` (11) | `Monk/Monk Red Animations/Monk_Red.controller`: `Monk_Idle_Red`, `Monk_Run_Red`, `Monk_Heal_Red` | 38 |
| Lancer | **320×320** | `Lancer/Lancer_Idle.png` (12), `Lancer_Run.png` (6), `Lancer_{Up,UpRight,Right,DownRight,Down}_Attack.png` (3 each), `…_Defence.png` (6 each) | `Lancer/Lancer Red Animations/Lancer_Red.controller`: `Lancer_Idle_Red`, `Lancer_ Run_Red`, `Lancer_ <Dir>_Attack_Red`, `Lancer_ <Dir>_Defence_Red` (**note the space after `Lancer_`** in most state/clip names) | 38 |
| Pawn | 192×192 | `Pawn and Resources/Pawn/Red Pawn/Pawn_Idle.png` (8) + `Pawn_Idle {Axe,Gold,Hammer,Knife,Meat,Pickaxe,Wood}.png` (8), `Pawn_Run[ X].png` (6), `Pawn_Interact Axe` (6) / `Hammer` (3) / `Knife` (4) / `Pickaxe` (6) | `Pawn Red Animations/Pawn_Red.controller`: `Pawn_Idle_Red` (default), `Pawn_Idle <Tool>_Red`, `Pawn_Run[ <Tool>]_Red`, `Pawn_Interact <Tool>_Red` (20 states) | 39 |

`Units/Enemy Pack - Promo/Enemy Pack_Promo.png` (1216×768) is a marketing image, not a gameplay sheet.
Scale: Tiny Swords is authored 1:1 at 64 PPU. For the promo look on the island, keep units/buildings/trees at **scale 1**
(a Warrior's body is about 1.2 cells tall). The base game's Warrior scale of 0.64 is a different art direction; don't mix them on one island.

---

## 7. Recommended editor-script approach

### 7.1 Verdict: (b) explicit lookup table, not RuleTile

RuleTile (tilemap.extras 6.0.1 is installed) can only express the **grass tops** (16 rules on "This/NotThis" in the same
tilemap). It **cannot** express the rest:
1. cliff cells are *extra* cells generated **outside** the painted footprint (one row below it);
2. ground vs water cliff depends on **another tilemap** (is there lower land under this cell?);
3. stairs connect on **one side only**, top and bottom pieces behave differently, and they live on the same tilemap as the tops;
4. shadow and foam are derived layers (offset by one cell / border-only).

A 60-line C# generator with the tables in section 4 is deterministic, testable (EditMode test: map → expected indices) and
matches the demo exactly. Use RuleTiles only if a designer wants to hand-paint grass tops later. Even then, generate
cliffs, shadow and foam with code.

### 7.2 Height-map format (screen-space, one char per 64 px cell, top line = highest y)

| char | meaning |
|---|---|
| `.` | water |
| `0` | flat ground (level 0) |
| `1` `2` `3` | **walkable top surface** of an elevated level at that cell (as it appears on screen) |
| `S` | stair **top** cell. Its level is decided by the horizontal neighbour with the higher level (East higher → `SL`, West higher → `SR`). The cell itself and the cell below (stair bottom, authored as `L-1`) are level L-1 ground. |
| (optional) `T` tree, `B` bush, `r` water rock, `H` house … | decoration markers, painted on the cell's level (resolve to `0`/`1`… for terrain) |

Important: the cliff row is **not** authored. Under the bottom edge of a `1` region, the next row must be authored as what
lies *behind/below* the cliff (`0` → ground cliff, `.` → water cliff). That row is not walkable (covered by the cliff).
Example (verified in the prototype):
```
......................
...0000000000000000...
..00111111111111000...
..00112222222111000...      L2 tops: rows 3-5, x 6..12
..00112222222111000...
..00112222222S11000...      S at x13: West=2 > East=1  -> SR stair of L2 (top next to L2's last grass row)
..00111111111111000...      row under L2 = L2 cliff row (authored as the L1 behind it); landing for the L2 stair
..00111111111111S00...      S at x16: West=1 > East=0  -> SR stair of L1
..00....00000000000...      L1 cliff row: x4..7 water -> C21/C22 water cliffs, rest ground cliffs; stair bottom at x16
.......000000000000...      landing for the L1 stair (x16)
...................0..      1x1 islet -> F16
......................
```

### 7.3 Algorithm (pseudo-C#)

```csharp
const string Dir = "Assets/Tiny Swords/Terrain/Tileset/Tilemap Settings/";
TileBase T(int color, int idx) => AssetDatabase.LoadAssetAtPath<TileBase>($"{Dir}Sliced Tiles/Tilemap_color{color}_{idx}.asset");
TileBase BG = Load(Dir + "Water Background color.asset"), FOAM = Load(Dir + "Water Tile animated.asset"), SHADOW = Load(Dir + "Shadow.asset");
int[] LevelColor = { 2, 1, 3, 4 };                 // flat, L1, L2, L3

// h[c]: -1 water, 0..maxL; stairs: top cell -> (L, kind SL|SR); stair top and bottom cells get h = L-1
for L in 0..maxL: land[L] = { c | h[c] >= L }       // higher levels count as land of lower levels (hidden underneath)

// Layer 2 Flat
foreach c in land[0]: Flat.SetTile(c, T(LevelColor[0], FlatByMask[Mask(c, land[0])]));

for L in 1..maxL:
    g = land[L]; stTop = stairs of level L; stBot = { s.top + S : s in stTop }
    foreach c in g:
        m = Mask(c, g);
        if (stTop.Is(c + E, SR)) m |= E;          // SR stands East of its plateau
        if (stTop.Is(c + W, SL)) m |= W;          // SL stands West of its plateau
        Elev[L].SetTile(c, T(LevelColor[L], ElevByMask[m]));
    cliffs = { c + S | c in g, c + S not in g };
    foreach k in cliffs:
        bool wo = g.Has(k+W) || cliffs.Has(k+W) || stBot.Is(k+W, SL);
        bool eo = g.Has(k+E) || cliffs.Has(k+E) || stBot.Is(k+E, SR);
        int i = (wo ? 1 : 0) | (eo ? 2 : 0);
        Elev[L].SetTile(k, T(LevelColor[L], h[k] < 0 ? CliffWater[i] : CliffGround[i]));
        if (h[k] >= 0 && h[k] < L - 1) Warn("2-step drop at " + k);  // only one cliff row is drawn
    foreach s in stTop:
        Elev[L].SetTile(s.top,     T(LevelColor[L], s.kind == SL ? 32 : 33));
        Elev[L].SetTile(s.top + S, T(LevelColor[L], s.kind == SL ? 38 : 39));
    foreach c in g ∪ stTop: Shadow[L].SetTile(c + S, SHADOW);

solid = land[0] ∪ { water-facing cliff cells of every level };
foreach c in solid where any 8-neighbour not in solid: Foam.SetTile(c, FOAM);
BG.BoxFill / SetTilesBlock over bounds + margin;
// optional: foreach foam cell: Foam.SetAnimationFrame(c, rng.Next(16));
```
`Mask(c, set)` = `(set.Has(c+N)?1:0) | (set.Has(c+E)?2:0) | (set.Has(c+S)?4:0) | (set.Has(c+W)?8:0)`, with `N = (0,+1)`.

Validation the script should report:
- a level-L region whose cliff row lands on level ≤ L-2 land (2-step drop: insert a band of L-1);
- a stair whose high-side neighbour is not level L, whose bottom cell is water, or whose landing `(x, y-2)` is not level L-1;
- a stair top directly under a level-L top cell;
- decorations or buildings on a cliff cell, a stair, or straddling two levels.

Walkability output (for gameplay): `walk(c) = h[c] >= 0 && c is not a cliff cell of any level`. The level of a walkable cell is `h[c]`.
Stair cells link L-1 and L. Movement between different levels is only allowed through stairs.

### 7.4 Layer creation order and settings

Create under one `Grid` (cellSize 1,1,0, cellLayout Rectangle). All Tilemaps at local (0,0,0), TileAnchor (0.5,0.5,0),
TilemapRenderer Mode **Chunk**, Detect Chunk Culling Bounds **Auto** (needed because foam and shadow sprites are 3× the cell),
same material as the project's other sprites. Only the `Default` sorting layer exists, and adding layers would change
Project Settings, so use order bands (with spacing so overlays can slot in):

| # | GameObject | sortingOrder (suggested) | demo order | content |
|---:|---|---:|---:|---|
| 0 | `L0 Water BG` | -100 | 0 | Water Background color |
| 1 | `L1 Water Foam` | -95 | 1 | Water Tile animated |
| — | water decor (SpriteRenderers) | -93 | — | Rocks in the Water 1–4, Rubber duck, boats |
| 2 | `L2 Flat Ground` | -90 | 2 | colour LevelColor[0] |
| 3 | `L3 Shadow (L1)` | -80 | 3 | Shadow |
| 4 | `L4 Elevated (L1)` | -70 | 4 | tops + cliffs + stairs of L1 |
| 5 | `L5 Shadow (L2)` | -60 | 5 | |
| 6 | `L6 Elevated (L2)` | -50 | 6 | |
| 7 | `L7 Shadow (L3)` | -40 | 7 | |
| 8 | `L8 Elevated (L3)` | -30 | 8 | |
| — | ground overlays (build plots, rings, markers) | -20…-1 | — | must be above the highest terrain layer |
| — | **Y-sorted objects** (trees, bushes, rocks, sheep, gold, buildings, units, FX) | `400 - round(feetY*10) + bias` (existing `IslandYSort`) | — | ≈200–600 for \|y\|<20 |
| — | clouds | ≥ 650 | — | below the existing 700/800 overlays in `IslandSceneSetup` |

The current `IslandSceneSetup` values (ground tilemap -20, decor -25, plots -15, misc -5) should be moved into this scheme.

---

## 8. Y-sorting objects against the elevated layers

### 8.1 Rule: all terrain is below all objects. Objects sort among themselves by feet Y.

In this pseudo-3D, a level-(L+1) plateau's top and cliff only overlap objects that stand **south** of it (in front of it).
Those objects must be drawn **over** the cliff. Objects north of a plateau (behind it) extend upward and don't overlap it.
So "terrain band < object band, objects Y-sorted by feet" is correct, as long as:
- objects only stand on walkable cells (never on a cliff cell, never "inside" a higher plateau's footprint);
- tall sprites (trees 3–4 cells high) stand at least one cell away from the top edge of a higher plateau directly south of them,
  otherwise the canopy would cover that plateau's grass;
- objects on a plateau are sorted by their on-screen feet Y like everything else (no per-level bias is needed). Optionally add
  `bias = +level` as a tie-breaker.

Known limitation: higher-level shadows don't darken objects (shadows are terrain). That's acceptable and matches the asset pack's demos.

### 8.2 Transparency sort settings

`Assets/Settings/Renderer2D.asset` has `m_TransparencySortMode: 0` (Default) with axis (0,1,0) set but inactive
(CustomAxis would be 3). `ProjectSettings/GraphicsSettings.asset` also has mode 0. The project sorts with explicit sortingOrder
(`IslandYSort`, `BuilderHut`, `HeroMovementController` all use `base - y*k`). Keep doing that. No settings change needed.

### 8.3 Feet offsets (all pivots are centre). Put the transform at the feet, child sprite at `+feet/64` units.

| Sprite | feet px | Sprite | feet px |
|---|---:|---|---:|
| Tree1 / Tree2 (192×256) | 113 / 121 | House1/2/3 | 69 / 77 / 70 |
| Tree3 / Tree4 (192×192) | 74 / 72 | Tower | 94 |
| Stump 1–2 / 3–4 | 112 / 72 | Archery / Barracks | 104 / 106 |
| Bush 1 / Bush 3 | 15 / 20 | Castle / Monastery | 121 / 141 |
| Rock1 / Rock4 | 19 / 24 | Warrior / Archer / Monk / Pawn | 41 / 40 / 38 / 39 |
| Water rocks 1 / 4 | 16 / 20 | Lancer (320 frame) | 38 |
| Sheep | 20 | Gold Stone 1 / 6 | 15 / 34 |

Simplest pattern: the parent GameObject at the feet position has `IslandYSort` and a `SortingGroup`. The child SpriteRenderer sits at
local `(0, feet/64)`. `IslandYSort` currently reads `GetComponent<SpriteRenderer>()` on the same object, so either
extend it to drive a SortingGroup or set the child's sortingOrder from the parent's Y.

---

## 9. Animation gotchas and how to drive decorations

- **Non-looping clips:** if you use the shipped controllers, bushes/trees/water rocks/units animate once and freeze.
  Options: (1) the project's own `IslandSpriteAnimator` / sprite-array approach (sprites copied into
  `Resources/IslandVisualCatalog.asset` by `Tools/Island/Build Island Catalog`). Set fps to **10** to match the art; the
  current `IslandSpriteAnimator.DefaultFps` is 8. (2) Have an editor script copy the needed clips into a project folder (e.g. `Assets/Art/Island/Anim/`) with
  `AnimationUtility.SetAnimationClipSettings(clip, settings{loopTime=true})`. Never edit `Assets/Tiny Swords/` in place.
- Desync idle loops: random start offset per instance (`IslandSpriteAnimator` already adds `Random.value*2` s), or
  `animator.Play(state, 0, Random.value)`.
- Controllers have no parameters/transitions: switch with `animator.Play("Sheep_Grass")` etc. Watch out for the odd names
  (`Tree 4 _Idle`, `Lancer_ Run_Red`, folder `FIre 3 Animation`).
- Runtime loading: `Assets/Tiny Swords` is not under `Resources`. Tilemaps painted by an **editor** script inside a scene or prefab
  reference the Tile assets directly and ship fine. Runtime generation needs the sprites/tiles referenced from a ScriptableObject
  (like `IslandVisualCatalog`): create `Tile`s at runtime with `ScriptableObject.CreateInstance<Tile>()`, and
  `AnimatedTile` for foam (tilemap.extras is a runtime assembly).

---

## 10. Promo-look composition checklist

1. Teal water everywhere (#47ABA9), with 4–8 cells of water margin visible around the island on a portrait screen.
2. A foam ring on every shoreline, frames desynced.
3. Flat ground in one colour, each elevation in a different colour (2 → 1 → 3 → 4). 1–2 elevated levels are enough; keep plateaus at least 2×2 tops.
4. Every plateau shows a stone cliff row along its south edge, water-facing (with foam) where it meets the sea.
5. A shadow layer under every elevated level. The 8 px dark halo around the cliff base sells the height.
6. At least one stair per plateau, on its east or west side, landing on walkable ground.
7. Tree clusters (mix Tree1–4, random idle phase) on the island's north and side edges and on plateau backs. Bushes along cliff bases and paths.
   A few land rocks. Gold stones near cliffs.
8. 2–4 sheep grazing (`Sheep_Grass` / `Sheep_Idle` alternating, occasional short `Sheep_Run` wander) on flat grass.
9. 3–6 animated water rocks scattered 1–3 cells off the shore. Optional rubber duck.
10. 2–4 clouds drifting slowly over the water margins (top layer, baked shadow).
11. Buildings at scale 1 on flat or L1 ground, with feet aligned to the footprint, Y-sorted with units and trees.

---

## Appendix: verification notes

- Slice rects, names and internal IDs: parsed from `Tilemap_color1..5.png.meta`. Colours 2–5 have identical index→rect layouts.
- Tile asset → sprite: all 220 `Tilemap_colorK_i.asset` reference the sprite `Tilemap_colorK_i` (fileID + guid match).
- Demo scene: all tilemaps parsed (`m_Tiles` → `m_TileAssetArray`/`m_TileSpriteArray`). Re-rendering them with the real sprites
  reproduces the island shown in guide image `_02` (same layout and pieces).
- Rules of section 4 predict 34/34 Layer 4, 14/14 Layer 6 and 4/4 Layer 8 tiles exactly.
- A Python prototype of section 7.3 (outside the repo) painted the height-map example in 7.2 with the demo colour mapping. Result:
  flat ring with foam, an L1 plateau whose cliff row is partly water-facing, an SR stair from L2 to L1, an SR stair from L1 to flat,
  shadows and a 1×1 islet. It renders cleanly from the real sprites.
