// Map renderer — draws terrain tiles and units on the canvas
'use strict';

const BASE_TILE = 24;
const MAP_W = 80;
const MAP_H = 50;
const ASSET_BASE = 'css/resources/';

// Terrain colours — used as a fallback while sprites are still loading.
// Indices match TerrainTypeEnum in OpenCivOne.
const TERRAIN_COLORS = [
  '#d4b870', // 0 = Desert
  '#c8a050', // 1 = Plains
  '#a0d060', // 2 = Grassland
  '#207830', // 3 = Forest
  '#c0a868', // 4 = Hills
  '#808080', // 5 = Mountains
  '#70b040', // 6 = Tundra
  '#f0f0f0', // 7 = Arctic
  '#507850', // 8 = Swamp
  '#308040', // 9 = Jungle
  '#1040a0', // 10 = Water/Ocean
  '#4080c0', // 11 = River
  '#d4b870', // 12 = ResourceOasis
  '#a0d060', // 13 = ResourceHorses
  '#a0d060', // 14 = ResourceGrassland
  '#207830', // 15 = ResourceGame
  '#808080', // 16 = ResourceCoal
  '#d4b870', // 17 = ResourceGold
  '#207830', // 18 = ResourceGame2
  '#f0f0f0', // 19 = ResourceSeals
  '#1040a0', // 20 = ResourceOil
  '#a0d060', // 21 = ResourceGems
  '#4080c0', // 22 = ResourceFish
  '#4080c0', // 23 = ResourceRiver
];

// Terrain sprite files, indices 0-11 match TerrainTypeEnum base terrain types.
const TERRAIN_SPRITE_FILES = [
  'desert.png', 'plains.png', 'grassland.png', 'forest.png', 'hills.png', 'mountains.png',
  'tundra.png', 'arctic.png', 'swamp.png', 'jungle.png', 'ocean.png', 'river.png',
];

// Resource variants (12-23) reuse the visual of their underlying base terrain
// (no dedicated resource artwork is provided) — same pairing as TERRAIN_COLORS above.
const RESOURCE_TERRAIN_BASE = [0, 2, 2, 3, 5, 0, 3, 7, 10, 2, 10, 11];
function terrainSpriteIndex(t) { return t >= 12 ? RESOURCE_TERRAIN_BASE[t - 12] : t; }

// TerrainImprovementFlagsEnum
const IMP_CITY      = 0x01;
const IMP_IRRIG     = 0x02;
const IMP_MINE      = 0x04;
const IMP_ROAD      = 0x08;
const IMP_RAILROAD  = 0x10;
const IMP_FORTRESS  = 0x20;
const IMP_POLLUTION = 0x40;

// UnitTypeEnum (0-27) -> sprite file
const UNIT_SPRITE_FILES = [
  'settler.png', 'militia.png', 'phalanx.png', 'legion.png', 'musketeer.png', 'riflemen.png',
  'cavalry.png', 'knight.png', 'catapult.png', 'cannon.png', 'chariot.png', 'armor.png',
  'mechinf.png', 'artillery.png', 'fighter.png', 'bomber.png', 'trireme.png', 'sail.png',
  'frigate.png', 'ironclad.png', 'cruiser.png', 'battleship.png', 'submarine.png', 'carrier.png',
  'transport.png', 'nuclear.png', 'diplomat.png', 'caravan.png',
];

// Civilization color by nationality string (case-insensitive prefix match).
// Pairs share the same color.
function getNationalityColor(nationality) {
  const n = (nationality || '').toLowerCase();
  if (n.startsWith('russian') || n.startsWith('roman'))    return '#FFAD14'; // yellow
  if (n.startsWith('indian')  || n.startsWith('mongol'))   return '#FFFFFF'; // white
  if (n.startsWith('zulu')    || n.startsWith('babylon'))  return '#606060'; // grey
  if (n.startsWith('chinese') || n.startsWith('american')) return '#D748D4'; // pink/purple
  if (n.startsWith('aztec')   || n.startsWith('egyptian')) return '#32FFFF'; // cyan
  if (n.startsWith('french')  || n.startsWith('german'))   return '#7C7CFF'; // blue/lavender
  if (n.startsWith('greek')   || n.startsWith('english'))  return '#02BF02'; // light green
  return '#FF0200'; // fallback
}

// --- Sprite loading -------------------------------------------------------
// All sprites are loaded up front. Renders before an image finishes loading
// fall back to flat colors/shapes (see render()); once each image loads we
// trigger a redraw so it pops in.
let _rendererForRedraw = null;
function _requestRedraw() { if (_rendererForRedraw) _rendererForRedraw.render(); }

function _loadImage(relPath) {
  const img = new Image();
  img.onload = _requestRedraw;
  img.src = ASSET_BASE + relPath;
  return img;
}

const TERRAIN_SPRITES = TERRAIN_SPRITE_FILES.map(f => _loadImage('terrain/' + f));
const UNIT_SPRITES = UNIT_SPRITE_FILES.map(f => _loadImage('unit/' + f));
const CITY_SPRITE = _loadImage('city/city.png');
const CITY_WITH_UNIT_SPRITE = _loadImage('city/city_with_unit.png');
const IMP_SPRITES = {
  [IMP_IRRIG]:     _loadImage('terrain/irrigation.png'),
  [IMP_MINE]:      _loadImage('terrain/mine.png'),
  [IMP_ROAD]:      _loadImage('terrain/route.png'),
  [IMP_RAILROAD]:  _loadImage('terrain/railroad.png'),
  [IMP_POLLUTION]: _loadImage('terrain/pollution.png'),
};

function _drawSprite(ctx, img, x, y, w, h) {
  if (img.complete && img.naturalWidth > 0) {
    ctx.drawImage(img, x, y, w, h);
    return true;
  }
  return false;
}

class MapRenderer {
  constructor(canvas) {
    this.canvas = canvas;
    this.ctx = canvas.getContext('2d');
    this.state = null;
    this.playerColors = {}; // playerID -> hex color
    _rendererForRedraw = this;

    // View is tracked as a pixel offset into the world (offsetX wraps around
    // the map's horizontal extent, offsetY is clamped — the map does not
    // wrap vertically).
    this.tileSize = BASE_TILE;
    this.minTileSize = BASE_TILE;
    this.maxTileSize = BASE_TILE * 4;
    this.offsetX = 0;
    this.offsetY = 0;
    this._centered = false;

    this._dragging = false;
    this._dragMoved = false;
    this._dragLastX = 0;
    this._dragLastY = 0;

    this._bindInteraction();
    this.resize();
  }

  // Recompute zoom bounds from the current canvas size. Call after the
  // canvas has been resized. At the minimum zoom level the whole map width
  // (80 tiles) exactly fills the canvas width.
  resize() {
    this.minTileSize = this.canvas.width / MAP_W;
    this.maxTileSize = Math.max(BASE_TILE * 4, this.minTileSize * 3);
    this.tileSize = Math.max(this.minTileSize, Math.min(this.maxTileSize, this.tileSize));
    this._normalizeOffsets();
    this.render();
  }

  setState(state) {
    this.state = state;

    // Build playerID -> color map from nationality
    this.playerColors = {};
    state?.players?.forEach(p => {
      this.playerColors[p.id] = getNationalityColor(p.nationality);
    });

    // Center the view on the human player's active unit, but only the very
    // first time we receive map data — after that the player controls the
    // view via drag/zoom and we must not snap it back on every state poll.
    if (!this._centered && state && state.map) {
      const human = state.units?.find(u => u.playerID === state.humanPlayerID);
      if (human) {
        this.offsetX = human.x * this.tileSize - this.canvas.width / 2;
        this.offsetY = human.y * this.tileSize - this.canvas.height / 2;
        this._normalizeOffsets();
        this._centered = true;
      }
    }
    this.render();
  }

  _playerColor(playerID) {
    return this.playerColors[playerID] ?? '#FF4040';
  }

  _normalizeOffsets() {
    const mapPxW = MAP_W * this.tileSize;
    this.offsetX = ((this.offsetX % mapPxW) + mapPxW) % mapPxW;
    const maxOffsetY = Math.max(0, MAP_H * this.tileSize - this.canvas.height);
    this.offsetY = Math.min(maxOffsetY, Math.max(0, this.offsetY));
  }

  // Converts a world tile coordinate to a screen-pixel coordinate, choosing
  // whichever wrap of the (horizontally toroidal) map lands on screen.
  _screenX(wx) {
    const ts = this.tileSize;
    const mapPxW = MAP_W * ts;
    let dx = ((wx * ts - this.offsetX) % mapPxW + mapPxW) % mapPxW;
    if (dx > this.canvas.width && dx - mapPxW >= -ts) dx -= mapPxW;
    return dx;
  }

  _screenY(wy) {
    return wy * this.tileSize - this.offsetY;
  }

  render() {
    if (!this.state?.map) return;
    const ctx = this.ctx;
    const { tiles } = this.state.map;
    const ts = this.tileSize;
    const s = ts / BASE_TILE; // scale factor for decorations drawn at fixed sizes

    ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);

    // --- Terrain ---
    const startTileX = Math.floor(this.offsetX / ts);
    const fracX = this.offsetX - startTileX * ts;
    const startTileY = Math.floor(this.offsetY / ts);
    const fracY = this.offsetY - startTileY * ts;
    const numCols = Math.ceil((this.canvas.width + fracX) / ts) + 1;
    const numRows = Math.ceil((this.canvas.height + fracY) / ts) + 1;

    for (let row = 0; row < numRows; row++) {
      const my = startTileY + row;
      if (my < 0 || my >= MAP_H) continue;
      const py = row * ts - fracY;

      for (let col = 0; col < numCols; col++) {
        const mx = ((startTileX + col) % MAP_W + MAP_W) % MAP_W;
        const px = col * ts - fracX;

        const tile = tiles[my]?.[mx];

        if (!tile || tile.v === 0) {
          ctx.fillStyle = '#000';
          ctx.fillRect(px, py, ts, ts);
          continue;
        }

        const sprite = TERRAIN_SPRITES[terrainSpriteIndex(tile.t)];
        if (!sprite || !_drawSprite(ctx, sprite, px, py, ts, ts)) {
          ctx.fillStyle = TERRAIN_COLORS[tile.t] ?? '#333';
          ctx.fillRect(px, py, ts, ts);
        }

        // --- Improvements (drawn as overlays on top of the terrain) ---
        const imp = tile.i;

        if (imp & IMP_RAILROAD) {
          if (!_drawSprite(ctx, IMP_SPRITES[IMP_RAILROAD], px, py, ts, ts)) {
            ctx.fillStyle = '#604020';
            ctx.fillRect(px + ts/2 - 2*s, py, 4*s, ts);
            ctx.fillRect(px, py + ts/2 - 2*s, ts, 4*s);
          }
        } else if (imp & IMP_ROAD) {
          if (!_drawSprite(ctx, IMP_SPRITES[IMP_ROAD], px, py, ts, ts)) {
            ctx.fillStyle = '#a08040';
            ctx.fillRect(px + ts/2 - 1*s, py, 2*s, ts);
            ctx.fillRect(px, py + ts/2 - 1*s, ts, 2*s);
          }
        }

        if (imp & IMP_IRRIG) {
          if (!_drawSprite(ctx, IMP_SPRITES[IMP_IRRIG], px, py, ts, ts)) {
            ctx.strokeStyle = '#4080ff';
            ctx.lineWidth = 1;
            ctx.strokeRect(px + 1.5, py + 1.5, ts - 3, ts - 3);
          }
        }

        if (imp & IMP_MINE) {
          if (!_drawSprite(ctx, IMP_SPRITES[IMP_MINE], px, py, ts, ts)) {
            ctx.fillStyle = '#555';
            ctx.fillRect(px + 4*s,  py + 4*s,  3*s, 3*s);
            ctx.fillRect(px + 17*s, py + 4*s,  3*s, 3*s);
            ctx.fillRect(px + 4*s,  py + 17*s, 3*s, 3*s);
            ctx.fillRect(px + 17*s, py + 17*s, 3*s, 3*s);
          }
        }

        if (imp & IMP_POLLUTION) {
          if (!_drawSprite(ctx, IMP_SPRITES[IMP_POLLUTION], px, py, ts, ts)) {
            ctx.fillStyle = '#333';
            ctx.beginPath();
            ctx.arc(px + ts/2, py + ts/2, ts/4, 0, Math.PI * 2);
            ctx.fill();
          }
        }

        // Fortress: small yellow square outline (no dedicated sprite provided)
        if (imp & IMP_FORTRESS) {
          ctx.strokeStyle = '#ffcc00';
          ctx.lineWidth = 2;
          ctx.strokeRect(px + 4*s, py + 4*s, ts - 8*s, ts - 8*s);
        }
      }
    }

    // --- Units (drawn before cities so cities appear on top) ---
    const drawnTiles = new Set();
    const cityPositions = new Set((this.state.cities || []).map(c => `${c.x},${c.y}`));

    this.state.units?.forEach(unit => {
      const px = this._screenX(unit.x);
      const py = this._screenY(unit.y);
      if (px < -ts || px > this.canvas.width || py < -ts || py > this.canvas.height) return;
      const tileRow = this.state.map?.tiles[unit.y];
      if (!tileRow || !tileRow[unit.x]?.v) return;
      if (cityPositions.has(`${unit.x},${unit.y}`)) return; // cities render their own garrison sprite

      const key = `${unit.x},${unit.y}`;
      if (drawnTiles.has(key)) return;
      drawnTiles.add(key);

      const st = unit.status;
      const isFortified  = !!(st & STATUS_FORTIFIED);
      const isFortifying = !!(st & STATUS_FORTIFYING);
      const isSleeping   = !!(st & STATUS_SENTRY);
      const isBuildIrr   = !!(st & STATUS_BUILD_IRR) && !(st & STATUS_BUILD_MINE);
      const isBuildMine  = !!(st & STATUS_BUILD_MINE) && !(st & STATUS_BUILD_IRR);
      const isBuildFort  = (st & 0xc0) === 0xc0;
      const isBuildRoad  = !!(st & STATUS_BUILD_ROAD) && !(st & 0xc0);

      const color = this._playerColor(unit.playerID);

      // Unit sprite, centered in the tile and scaled to fit while preserving
      // its aspect ratio. The sprite's transparent pixels are replaced by a
      // solid civilization-color backdrop (drawn first, sprite on top) —
      // the sprite's own opaque pixels are left untouched, unfaded.
      const sprite = UNIT_SPRITES[unit.type];
      let drewSprite = false;
      if (sprite && sprite.complete && sprite.naturalWidth > 0) {
        const boxW = ts - 4*s, boxH = ts - 4*s;
        const scale = Math.min(boxW / sprite.naturalWidth, boxH / sprite.naturalHeight);
        const dw = sprite.naturalWidth * scale, dh = sprite.naturalHeight * scale;
        const dx = px + (ts - dw) / 2, dy = py + (ts - dh) / 2;
        ctx.fillStyle = color;
        ctx.fillRect(dx, dy, dw, dh);
        ctx.drawImage(sprite, dx, dy, dw, dh);
        drewSprite = true;
      }

      if (!drewSprite) {
        ctx.fillStyle = color;
        ctx.fillRect(px + 1*s, py + 1*s, ts - 2*s, ts - 2*s);
        const isDark = _colorIsDark(color);
        ctx.fillStyle = isDark ? '#eee' : '#000';
        ctx.font = `bold ${Math.max(8, 10*s)}px monospace`;
        ctx.fillText((unit.name || '?')[0], px + 4*s, py + 11*s);
      }

      // Current action — big white letter overlay, outlined for legibility
      // over any sprite/terrain.
      let actionLabel = null;
      if (isFortified || isFortifying) actionLabel = 'F';
      else if (isSleeping)  actionLabel = 'Z';
      else if (isBuildFort) actionLabel = 'F';
      else if (isBuildIrr)  actionLabel = 'I';
      else if (isBuildMine) actionLabel = 'M';
      else if (isBuildRoad) actionLabel = 'R';

      if (actionLabel) {
        ctx.font = `bold ${Math.max(16, 15*s)}px monospace`;
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.lineWidth = 3;
        ctx.strokeStyle = '#000';
        ctx.strokeText(actionLabel, px + ts/2, py + ts/2);
        ctx.fillStyle = '#fff';
        ctx.fillText(actionLabel, px + ts/2, py + ts/2);
        ctx.textAlign = 'left';
        ctx.textBaseline = 'alphabetic';
      }
    });

    // --- Cities (drawn after units so they always appear on top) ---
    this.state.cities?.forEach(city => {
      const px = this._screenX(city.x);
      const py = this._screenY(city.y);
      if (px < -ts || px > this.canvas.width || py < -ts || py > this.canvas.height) return;

      const color = this._playerColor(city.playerID);
      ctx.fillStyle = color;
      ctx.fillRect(px + 2*s, py + 2*s, ts - 4*s, ts - 4*s);

      const hasUnit = this.state.units?.some(u => u.x === city.x && u.y === city.y);
      const sprite = hasUnit ? CITY_WITH_UNIT_SPRITE : CITY_SPRITE;
      _drawSprite(ctx, sprite, px + 2*s, py + 2*s, ts - 4*s, ts - 4*s);

      // City size number — centered in the tile, both axes
      ctx.font = `bold ${Math.max(16, 14*s)}px monospace`;
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.lineWidth = 3;
      ctx.strokeStyle = '#000';
      ctx.strokeText(city.size, px + ts / 2, py + ts / 2 + 2);
      ctx.fillStyle = '#fff';
      ctx.fillText(city.size, px + ts / 2, py + ts / 2 + 2);

      // City name below the tile — centered horizontally on the tile
      const name = city.name || '';
      ctx.font = `bold ${Math.max(16, 10*s)}px monospace`;
      ctx.fillStyle = '#fff';
      ctx.strokeStyle = '#000';
      ctx.lineWidth = 2;
      ctx.textBaseline = 'alphabetic';
      ctx.strokeText(name, px + ts / 2, py + ts + 7*s);
      ctx.fillText(name, px + ts / 2, py + ts + 7*s);

      ctx.textAlign = 'left';
      ctx.textBaseline = 'alphabetic';
    });

    // --- Highlight active human unit ---
    const activeUnit = this.state.units?.find(
      u => u.playerID === this.state.humanPlayerID &&
           !(u.status & (STATUS_SENTRY | STATUS_FORTIFIED | STATUS_FORTIFYING | 0xc2)) &&
           u.moves > 0
    );
    if (activeUnit) {
      const px = this._screenX(activeUnit.x);
      const py = this._screenY(activeUnit.y);
      if (px >= -ts && px < this.canvas.width && py >= -ts && py < this.canvas.height) {
        ctx.strokeStyle = '#ffffff';
        ctx.lineWidth = 2;
        ctx.strokeRect(px + 1*s, py + 1*s, ts - 2*s, ts - 2*s);
        ctx.lineWidth = 1;
      }
    }
  }

  clickToTile(clientX, clientY) {
    const rect = this.canvas.getBoundingClientRect();
    const px = clientX - rect.left;
    const py = clientY - rect.top;
    const wx = Math.floor((this.offsetX + px) / this.tileSize);
    const wy = Math.floor((this.offsetY + py) / this.tileSize);
    return {
      x: ((wx % MAP_W) + MAP_W) % MAP_W,
      y: wy
    };
  }

  // Nudges the view by a number of tiles (used for keyboard scrolling).
  scroll(dxTiles, dyTiles) {
    this.offsetX += dxTiles * this.tileSize;
    this.offsetY += dyTiles * this.tileSize;
    this._normalizeOffsets();
    this.render();
  }

  zoomAt(clientX, clientY, deltaY) {
    const rect = this.canvas.getBoundingClientRect();
    const px = clientX - rect.left;
    const py = clientY - rect.top;

    // World tile coordinates under the cursor, before zooming.
    const worldTileX = (this.offsetX + px) / this.tileSize;
    const worldTileY = (this.offsetY + py) / this.tileSize;

    const factor = deltaY < 0 ? 1.15 : (1 / 1.15);
    const newTileSize = Math.max(this.minTileSize, Math.min(this.maxTileSize, this.tileSize * factor));
    if (newTileSize === this.tileSize) return;
    this.tileSize = newTileSize;

    // Keep the same world point under the cursor after zooming.
    this.offsetX = worldTileX * this.tileSize - px;
    this.offsetY = worldTileY * this.tileSize - py;
    this._normalizeOffsets();
    this.render();
  }

  // Drag-to-pan. Wired up internally so main.js doesn't need to know about it,
  // except to avoid treating a drag as a tile-info click (see consumeDragFlag).
  _bindInteraction() {
    this.canvas.addEventListener('wheel', e => {
      e.preventDefault();
      this.zoomAt(e.clientX, e.clientY, e.deltaY);
    }, { passive: false });

    this.canvas.addEventListener('mousedown', e => {
      if (e.button !== 0) return;
      this._dragging = true;
      this._dragMoved = false;
      this._dragLastX = e.clientX;
      this._dragLastY = e.clientY;
      this.canvas.style.cursor = 'grabbing';
    });

    window.addEventListener('mousemove', e => {
      if (!this._dragging) return;
      const dx = e.clientX - this._dragLastX;
      const dy = e.clientY - this._dragLastY;
      if (Math.abs(dx) > 2 || Math.abs(dy) > 2) this._dragMoved = true;
      this.offsetX -= dx;
      this.offsetY -= dy;
      this._dragLastX = e.clientX;
      this._dragLastY = e.clientY;
      this._normalizeOffsets();
      this.render();
    });

    window.addEventListener('mouseup', () => {
      if (!this._dragging) return;
      this._dragging = false;
      this.canvas.style.cursor = 'grab';
    });
  }

  // Returns true (and clears the flag) if the last mouse-up ended a drag —
  // callers use this to suppress click-to-inspect after panning the map.
  consumeDragFlag() {
    const moved = this._dragMoved;
    this._dragMoved = false;
    return moved;
  }
}

// Returns true if a hex color is perceptually dark (better to use light text on it).
function _colorIsDark(hex) {
  const r = parseInt(hex.slice(1, 3), 16);
  const g = parseInt(hex.slice(3, 5), 16);
  const b = parseInt(hex.slice(5, 7), 16);
  return (r * 299 + g * 587 + b * 114) / 1000 < 128;
}
