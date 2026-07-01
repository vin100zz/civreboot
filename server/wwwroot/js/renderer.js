// Map renderer — draws terrain tiles and units on the canvas
'use strict';

const TILE_W = 24;
const TILE_H = 24;
const MAP_W = 80;
const MAP_H = 50;

// Terrain colours — indices match TerrainTypeEnum in OpenCivOne
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

// TerrainImprovementFlagsEnum
const IMP_CITY      = 0x01;
const IMP_IRRIG     = 0x02;
const IMP_MINE      = 0x04;
const IMP_ROAD      = 0x08;
const IMP_RAILROAD  = 0x10;
const IMP_FORTRESS  = 0x20;

// Civilization color by nationality string (case-insensitive prefix match).
// Pairs share the same color.
function getNationalityColor(nationality) {
  const n = (nationality || '').toLowerCase();
  if (n.startsWith('russian') || n.startsWith('roman'))    return '#FFD700'; // yellow
  if (n.startsWith('indian')  || n.startsWith('mongol'))   return '#FFFFFF'; // white
  if (n.startsWith('zulu')    || n.startsWith('babylon'))  return '#AAAAAA'; // grey
  if (n.startsWith('chinese') || n.startsWith('american')) return '#FF80FF'; // pink/purple
  if (n.startsWith('aztec')   || n.startsWith('egyptian')) return '#00E0E0'; // cyan
  if (n.startsWith('french')  || n.startsWith('german'))   return '#9090FF'; // blue/lavender
  if (n.startsWith('greek')   || n.startsWith('english'))  return '#80FF80'; // light green
  return '#FF4040'; // fallback
}

class MapRenderer {
  constructor(canvas) {
    this.canvas = canvas;
    this.ctx = canvas.getContext('2d');
    this.state = null;
    this.viewX = 0;
    this.viewY = 0;
    this.tilesX = Math.floor(canvas.width / TILE_W);
    this.tilesY = Math.floor(canvas.height / TILE_H);
    this.playerColors = {}; // playerID -> hex color
  }

  setState(state) {
    this.state = state;

    // Build playerID -> color map from nationality
    this.playerColors = {};
    state?.players?.forEach(p => {
      this.playerColors[p.id] = getNationalityColor(p.nationality);
    });

    if (state && state.map) {
      const human = state.units?.find(u => u.playerID === state.humanPlayerID);
      if (human) {
        this.viewX = Math.max(0, human.x - Math.floor(this.tilesX / 2));
        this.viewY = Math.max(0, human.y - Math.floor(this.tilesY / 2));
      }
    }
    this.render();
  }

  _playerColor(playerID) {
    return this.playerColors[playerID] ?? '#FF4040';
  }

  render() {
    if (!this.state?.map) return;
    const ctx = this.ctx;
    const { tiles } = this.state.map;

    // --- Terrain ---
    for (let ty = 0; ty < this.tilesY; ty++) {
      for (let tx = 0; tx < this.tilesX; tx++) {
        const mx = (this.viewX + tx) % MAP_W;
        const my = this.viewY + ty;
        if (my < 0 || my >= MAP_H) continue;

        const tile = tiles[my]?.[mx];
        const px = tx * TILE_W;
        const py = ty * TILE_H;

        if (!tile || tile.v === 0) {
          ctx.fillStyle = '#000';
          ctx.fillRect(px, py, TILE_W, TILE_H);
          continue;
        }

        ctx.fillStyle = TERRAIN_COLORS[tile.t] ?? '#333';
        ctx.fillRect(px, py, TILE_W, TILE_H);

        // --- Improvements ---
        const imp = tile.i;

        // Railroad: thick brown cross
        if (imp & IMP_RAILROAD) {
          ctx.fillStyle = '#604020';
          ctx.fillRect(px + TILE_W/2 - 2, py, 4, TILE_H);
          ctx.fillRect(px, py + TILE_H/2 - 2, TILE_W, 4);
        }
        // Road: thin tan cross
        else if (imp & IMP_ROAD) {
          ctx.fillStyle = '#a08040';
          ctx.fillRect(px + TILE_W/2 - 1, py, 2, TILE_H);
          ctx.fillRect(px, py + TILE_H/2 - 1, TILE_W, 2);
        }

        // Irrigation: blue border
        if (imp & IMP_IRRIG) {
          ctx.strokeStyle = '#4080ff';
          ctx.lineWidth = 1;
          ctx.strokeRect(px + 1.5, py + 1.5, TILE_W - 3, TILE_H - 3);
        }

        // Mine: dark grey dots (2×2 grid)
        if (imp & IMP_MINE) {
          ctx.fillStyle = '#555';
          ctx.fillRect(px + 4,  py + 4,  3, 3);
          ctx.fillRect(px + 17, py + 4,  3, 3);
          ctx.fillRect(px + 4,  py + 17, 3, 3);
          ctx.fillRect(px + 17, py + 17, 3, 3);
        }

        // Fortress: small yellow square outline
        if (imp & IMP_FORTRESS) {
          ctx.strokeStyle = '#ffcc00';
          ctx.lineWidth = 2;
          ctx.strokeRect(px + 4, py + 4, TILE_W - 8, TILE_H - 8);
        }
      }
    }

    // --- Units (drawn before cities so cities appear on top) ---
    const drawnTiles = new Set();

    this.state.units?.forEach(unit => {
      const tx = ((unit.x - this.viewX) + MAP_W) % MAP_W;
      const ty = unit.y - this.viewY;
      if (tx < 0 || tx >= this.tilesX || ty < 0 || ty >= this.tilesY) return;
      const tileRow = this.state.map?.tiles[unit.y];
      if (!tileRow || !tileRow[unit.x]?.v) return;

      const key = `${tx},${ty}`;
      if (drawnTiles.has(key)) return;
      drawnTiles.add(key);

      const s = unit.status;
      const isFortified  = !!(s & STATUS_FORTIFIED);
      const isFortifying = !!(s & STATUS_FORTIFYING);
      const isSleeping   = !!(s & STATUS_SENTRY);
      const isBuildIrr   = !!(s & STATUS_BUILD_IRR) && !(s & STATUS_BUILD_MINE);
      const isBuildMine  = !!(s & STATUS_BUILD_MINE) && !(s & STATUS_BUILD_IRR);
      const isBuildFort  = (s & 0xc0) === 0xc0;
      const isBuildRoad  = !!(s & STATUS_BUILD_ROAD) && !(s & 0xc0);

      const isPassive = isFortified || isFortifying || isSleeping ||
                        isBuildIrr || isBuildMine || isBuildFort || isBuildRoad;

      const px = tx * TILE_W + 1;
      const py = ty * TILE_H + 1;
      const color = this._playerColor(unit.playerID);

      ctx.fillStyle = color;
      ctx.fillRect(px, py, TILE_W - 2, TILE_H - 2);

      // Label — dark text on light colors, light text on dark colors
      const isDark = _colorIsDark(color);
      ctx.fillStyle = isDark ? '#eee' : '#000';
      ctx.font = 'bold 10px monospace';

      let label = (unit.name || '?')[0];
      if (isFortified || isFortifying) label = 'F';
      else if (isSleeping)   label = 'Z';
      else if (isBuildFort)  label = 'f';
      else if (isBuildIrr)   label = 'I';
      else if (isBuildMine)  label = 'M';
      else if (isBuildRoad)  label = 'R';

      ctx.fillText(label, px + 4, py + 11);

      // Small status dot bottom-right
      if (isFortified) { ctx.fillStyle = '#fff'; ctx.fillRect(px + TILE_W - 7, py + TILE_H - 7, 4, 4); }
      if (isSleeping)  { ctx.fillStyle = '#88f'; ctx.fillRect(px + TILE_W - 7, py + TILE_H - 7, 4, 4); }
    });

    // --- Cities (drawn after units so they always appear on top) ---
    this.state.cities?.forEach(city => {
      const tx = ((city.x - this.viewX) + MAP_W) % MAP_W;
      const ty = city.y - this.viewY;
      if (tx < 0 || tx >= this.tilesX || ty < 0 || ty >= this.tilesY) return;
      const px = tx * TILE_W;
      const py = ty * TILE_H;

      const color = this._playerColor(city.playerID);
      ctx.fillStyle = color;
      ctx.fillRect(px + 2, py + 2, TILE_W - 4, TILE_H - 4);

      // City size number
      const isDark = _colorIsDark(color);
      ctx.fillStyle = isDark ? '#eee' : '#000';
      ctx.font = '9px monospace';
      ctx.fillText(city.size, px + 5, py + 12);

      // City name below the tile
      const name = city.name || '';
      ctx.font = '7px monospace';
      ctx.fillStyle = '#fff';
      ctx.strokeStyle = '#000';
      ctx.lineWidth = 2;
      ctx.strokeText(name, px + 1, py + TILE_H + 8);
      ctx.fillText(name, px + 1, py + TILE_H + 8);
    });

    // --- Highlight active human unit ---
    const activeUnit = this.state.units?.find(
      u => u.playerID === this.state.humanPlayerID &&
           !(u.status & (STATUS_SENTRY | STATUS_FORTIFIED | STATUS_FORTIFYING | 0xc2)) &&
           u.moves > 0
    );
    if (activeUnit) {
      const tx = ((activeUnit.x - this.viewX) + MAP_W) % MAP_W;
      const ty = activeUnit.y - this.viewY;
      if (tx >= 0 && tx < this.tilesX && ty >= 0 && ty < this.tilesY) {
        ctx.strokeStyle = '#ffffff';
        ctx.lineWidth = 2;
        ctx.strokeRect(tx * TILE_W + 1, ty * TILE_H + 1, TILE_W - 2, TILE_H - 2);
        ctx.lineWidth = 1;
      }
    }
  }

  clickToTile(clientX, clientY) {
    const rect = this.canvas.getBoundingClientRect();
    const tx = Math.floor((clientX - rect.left)  / TILE_W);
    const ty = Math.floor((clientY - rect.top)   / TILE_H);
    return {
      x: (this.viewX + tx) % MAP_W,
      y: this.viewY + ty
    };
  }

  scroll(dx, dy) {
    this.viewX = ((this.viewX + dx) + MAP_W) % MAP_W;
    this.viewY = Math.max(0, Math.min(MAP_H - this.tilesY, this.viewY + dy));
    this.render();
  }
}

// Returns true if a hex color is perceptually dark (better to use light text on it).
function _colorIsDark(hex) {
  const r = parseInt(hex.slice(1, 3), 16);
  const g = parseInt(hex.slice(3, 5), 16);
  const b = parseInt(hex.slice(5, 7), 16);
  return (r * 299 + g * 587 + b * 114) / 1000 < 128;
}
