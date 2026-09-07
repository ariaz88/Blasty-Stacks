// cpcalc.js — reimplements Unity AnimationCurve.Evaluate + ProgressionMath + CPWeightMath +
// CPCalculator exactly as the project's C# does, reading the REAL .asset YAML.
// Read-only analysis script: it writes out.json beside itself and touches nothing in the project.
//
// Run:  node Docs/cp-analysis/tools/cpcalc.js
// Requires Node 18+. No dependencies, no install step, no Unity.

const fs = require('fs');
const path = require('path');

// Repo root is three levels up from Docs/cp-analysis/tools/ — keep this file at that depth,
// or override with:  CP_REPO_ROOT=/path/to/repo node cpcalc.js
const ROOT = process.env.CP_REPO_ROOT || path.resolve(__dirname, '..', '..', '..');
const ASSETS = path.join(ROOT, 'Assets');

if (!fs.existsSync(ASSETS)) {
  console.error('Cannot find an Assets/ folder at: ' + ASSETS);
  console.error('Run this from inside the repo, or set CP_REPO_ROOT to the repo root.');
  process.exit(1);
}

// ---------------------------------------------------------------- guid index
function walk(dir, out = []) {
  let entries;
  try { entries = fs.readdirSync(dir, { withFileTypes: true }); } catch { return out; }
  for (const e of entries) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) walk(p, out);
    else out.push(p);
  }
  return out;
}

const allFiles = walk(path.join(ASSETS, 'Scriptable Objects'))
  .concat(walk(path.join(ASSETS, 'Scripts', 'UI', 'UI-SOs')));

const guidToPath = {};
for (const f of allFiles) {
  if (!f.endsWith('.meta')) continue;
  const txt = fs.readFileSync(f, 'utf8');
  const m = txt.match(/^guid:\s*([0-9a-f]{32})/m);
  if (m) guidToPath[m[1]] = f.slice(0, -5); // strip .meta
}

// ---------------------------------------------------------------- YAML curve parsing
// Extracts every "  <name>:\n    serializedVersion: 2\n    m_Curve:\n    - ..." block.
function parseCurves(text) {
  const curves = {};
  const lines = text.split(/\r?\n/);
  let i = 0;
  while (i < lines.length) {
    const m = lines[i].match(/^  ([A-Za-z0-9_]+):\s*$/);
    if (!m) { i++; continue; }
    const name = m[1];
    // Expect serializedVersion then m_Curve
    if (!/^\s+serializedVersion:/.test(lines[i + 1] || '') || !/^\s+m_Curve:/.test(lines[i + 2] || '')) {
      i++; continue;
    }
    const keys = [];
    let j = i + 3;
    let cur = null;
    let preInf = 2, postInf = 2;
    for (; j < lines.length; j++) {
      const L = lines[j];
      if (/^  [A-Za-z0-9_]+:/.test(L)) break;          // next top-level field
      const km = L.match(/^\s+-\s+serializedVersion:/);
      if (km) { cur = {}; keys.push(cur); continue; }
      const kv = L.match(/^\s+(time|value|inSlope|outSlope|tangentMode|weightedMode|inWeight|outWeight):\s*(-?[\d.eE+-]+)/);
      if (kv && cur) { cur[kv[1]] = parseFloat(kv[2]); continue; }
      const pre = L.match(/^\s+m_PreInfinity:\s*(-?\d+)/);
      if (pre) { preInf = parseInt(pre[1], 10); continue; }
      const post = L.match(/^\s+m_PostInfinity:\s*(-?\d+)/);
      if (post) { postInf = parseInt(post[1], 10); continue; }
    }
    if (keys.length) curves[name] = { keys, preInf, postInf };
    i = j;
  }
  return curves;
}

// Unity AnimationCurve.Evaluate. PreInfinity/PostInfinity mode 2 == Clamp (the only mode
// present in these assets). Unweighted cubic Hermite between keys.
function evaluate(curve, t) {
  if (!curve || !curve.keys.length) return 0;
  const k = curve.keys;
  if (k.length === 1) return k[0].value;
  if (t <= k[0].time) return k[0].value;                       // clamp
  if (t >= k[k.length - 1].time) return k[k.length - 1].value; // clamp
  let a = 0;
  for (let i = 0; i < k.length - 1; i++) if (t >= k[i].time && t <= k[i + 1].time) { a = i; break; }
  const k0 = k[a], k1 = k[a + 1];
  const dt = k1.time - k0.time;
  if (dt === 0) return k0.value;
  const u = (t - k0.time) / dt;
  const m0 = (k0.outSlope || 0) * dt;
  const m1 = (k1.inSlope || 0) * dt;
  const u2 = u * u, u3 = u2 * u;
  return (2 * u3 - 3 * u2 + 1) * k0.value
       + (u3 - 2 * u2 + u) * m0
       + (-2 * u3 + 3 * u2) * k1.value
       + (u3 - u2) * m1;
}

// A curve created by AnimationCurve.Linear(t0,v0,t1,v1) — the C# field initializer, used when
// a curve is ABSENT from the serialized YAML (older asset written before the field existed).
function linearCurve(t0, v0, t1, v1) {
  const slope = (v1 - v0) / (t1 - t0);
  return { keys: [
    { time: t0, value: v0, inSlope: 0, outSlope: slope },
    { time: t1, value: v1, inSlope: slope, outSlope: 0 },
  ], preInf: 2, postInf: 2, _synthesized: true };
}

const clamp = (v, lo, hi) => Math.min(hi, Math.max(lo, v));

// ---------------------------------------------------------------- ProgressionMath
// Growth = product over l=2..L of (1 + clamp(pct(l), pctClamp))
function getGrowth(level, cfg) {
  const g = { gA: 1, gH: 1, gMv: 1, gAS: 1, gD: 1, gR: 1 };
  if (!cfg || level <= 1) return g;
  const [lo, hi] = cfg.pctClamp;
  for (let l = 2; l <= level; l++) {
    g.gA  *= 1 + clamp(evaluate(cfg.atkPctByLevel, l), lo, hi);
    g.gH  *= 1 + clamp(evaluate(cfg.hpPctByLevel, l), lo, hi);
    g.gMv *= 1 + clamp(evaluate(cfg.movePctByLevel, l), lo, hi);
    g.gAS *= 1 + clamp(evaluate(cfg.atkSpdPctByLevel, l), lo, hi);
    g.gD  *= 1 + clamp(evaluate(cfg.defPctByLevel, l), lo, hi);
    g.gR  *= 1 + clamp(evaluate(cfg.rangePctByLevel, l), lo, hi);
  }
  return g;
}

// ---------------------------------------------------------------- CPWeightMath.Evaluate
// Faithful to CPWeightMath.cs INCLUDING the bug: meleeMult is never read from cfg, and
// rangedMult is assigned twice. Pass buggy=false to model the intended behaviour.
function evalWeights(level, cfg, buggy = true) {
  const w = { wA: 1, wH: 0.15, wMv: 0.25, wAS: 0.40, wD: 0, wR: 0.05, meleeMult: 1, rangedMult: 1.05 };
  if (!cfg) return w;
  const L = Math.max(1, level);
  const [cMin, cMax] = cfg.wClamp;
  const s = clamp(evaluate(cfg.globalWeightScaleByLevel, L), 1, 10);
  w.wA  = clamp(evaluate(cfg.wAttackByLevel, L),      cMin, cMax) * s;
  w.wH  = clamp(evaluate(cfg.wHPByLevel, L),          cMin, cMax) * s;
  w.wMv = clamp(evaluate(cfg.wMoveSpeedByLevel, L),   cMin, cMax) * s;
  w.wAS = clamp(evaluate(cfg.wAttackSpeedByLevel, L), cMin, cMax) * s;
  w.wD  = clamp(evaluate(cfg.wDefenseByLevel, L),     cMin, cMax) * s;
  w.wR  = clamp(evaluate(cfg.wRangeByLevel, L),       cMin, cMax) * s;
  w.rangedMult = clamp(evaluate(cfg.rangedMultByLevel, L), 1, 5);
  if (!buggy) w.meleeMult = clamp(evaluate(cfg.meleeMultByLevel, L), 1, 5);
  // buggy==true: the melee line is MISSING in C#; meleeMult keeps its 1f default.
  return w;
}

// ---------------------------------------------------------------- CPCalculator.UnitCP
const RANGED = new Set([1, 3]); // FighterType.Archer=1, Mage=3
function unitCP(s, level, cfg, buggy = true) {
  const w = evalWeights(level, cfg, buggy);
  const base = w.wA * s.attack + w.wH * s.maxHP + w.wMv * s.moveSpeed
             + w.wAS * s.attackSpeed + w.wD * s.defense + w.wR * s.attackRange;
  const typeMult = RANGED.has(s.type) ? w.rangedMult : w.meleeMult;
  return Math.round(base * typeMult);
}

// ---------------------------------------------------------------- load configs
function loadCPWeights(p) {
  const c = parseCurves(fs.readFileSync(p, 'utf8'));
  const txt = fs.readFileSync(p, 'utf8');
  const cm = txt.match(/wClamp:\s*\{x:\s*(-?[\d.]+),\s*y:\s*(-?[\d.]+)\}/);
  return {
    _path: p, _present: Object.keys(c),
    wAttackByLevel: c.wAttackByLevel, wHPByLevel: c.wHPByLevel,
    wMoveSpeedByLevel: c.wMoveSpeedByLevel, wAttackSpeedByLevel: c.wAttackSpeedByLevel,
    wDefenseByLevel: c.wDefenseByLevel,
    // C# initializer fallback when the field is absent from the YAML:
    wRangeByLevel: c.wRangeByLevel || linearCurve(1, 0.05, 50, 0.04),
    meleeMultByLevel: c.meleeMultByLevel || linearCurve(1, 1.00, 50, 1.00),
    rangedMultByLevel: c.rangedMultByLevel || linearCurve(1, 1.05, 50, 1.05),
    globalWeightScaleByLevel: c.globalWeightScaleByLevel || linearCurve(1, 1, 50, 1),
    wClamp: cm ? [parseFloat(cm[1]), parseFloat(cm[2])] : [-10, 10],
  };
}

function loadProgression(p) {
  const txt = fs.readFileSync(p, 'utf8');
  const c = parseCurves(txt);
  const cm = txt.match(/pctClamp:\s*\{x:\s*(-?[\d.]+),\s*y:\s*(-?[\d.]+)\}/);
  return {
    _path: p, _present: Object.keys(c),
    atkPctByLevel: c.atkPctByLevel, hpPctByLevel: c.hpPctByLevel,
    movePctByLevel: c.movePctByLevel, atkSpdPctByLevel: c.atkSpdPctByLevel,
    defPctByLevel: c.defPctByLevel || linearCurve(1, 0.02, 50, 0.005),
    rangePctByLevel: c.rangePctByLevel || linearCurve(1, 0.01, 50, 0.003),
    pctClamp: cm ? [parseFloat(cm[1]), parseFloat(cm[2])] : [-0.25, 0.5],
  };
}

function loadStats(p) {
  const t = fs.readFileSync(p, 'utf8');
  const num = (k, d) => { const m = t.match(new RegExp('^\\s{2}' + k + ':\\s*(-?[\\d.eE+-]+)', 'm')); return m ? parseFloat(m[1]) : d; };
  return {
    name: path.basename(p, '.asset'),
    attack: num('attack', 0), defense: num('defense', 0), maxHP: num('maxHP', 0),
    attackSpeed: num('attackSpeed', 0), moveSpeed: num('moveSpeed', 0),
    attackRange: num('attackRange', 0), type: num('type', 0),
  };
}

const SO = path.join(ASSETS, 'Scriptable Objects');
const playerCP   = loadCPWeights(path.join(SO, 'CP', 'Player CP.asset'));
const enemyCP    = loadCPWeights(path.join(SO, 'CP', 'EnemyCP.asset'));
const playerProg = loadProgression(path.join(SO, 'Stats', 'Progression', 'PlayerProgressionConfig.asset'));
const enemyProg  = loadProgression(path.join(SO, 'Stats', 'Progression', 'EnemyProgression.asset'));

// ---------------------------------------------------------------- rosters
const HERO_DIR = path.join(SO, 'Stats', 'BaseStats', 'New ChratersSTats');
const ENEMY_DIR = path.join(SO, 'Stats', 'BaseStats', 'Enemy Base Stats');

// unitId -> { displayName, statsAsset } taken from the 8 UnitDefinitionSO assets in
// UnitsDatabaseSO order (verified by reading the DefSO assets).
const HEROES = [
  { id: 1, name: 'Valkir3',            stats: 'Player_Minotaur_01' },
  { id: 2, name: 'Dark_Oracle_1',      stats: 'Dark_Oracle_01' },
  { id: 3, name: 'Minotaur_02',        stats: 'Minotaur_02' },
  { id: 4, name: 'Minotaur_2',         stats: 'CowMinotaur_2' },
  { id: 5, name: 'Golem_3',            stats: 'Golem_3' },
  { id: 6, name: 'Fallen_Angels_02',   stats: 'PlayerValkir3' },
  { id: 7, name: 'Fallen_Minotaur_01', stats: 'Player_Minotaur_01' },
  { id: 8, name: 'Dark_Oracle_3',      stats: 'Player_Dark_Oracle_3' },
];
for (const h of HEROES) h.base = loadStats(path.join(HERO_DIR, h.stats + '.asset'));

const ENEMY_NAMES = ['Enemy_Reaper_Man_01', 'Enemy_Zombie_villager', 'Enemy_Orc',
                     'Enemy_Skeleton_Crusader_1', 'Enemy_Golem_01', 'Enemy_Golem_02'];
const ENEMIES = ENEMY_NAMES.map(n => ({ name: n, base: loadStats(path.join(ENEMY_DIR, n + '.asset')) }));

// ---------------------------------------------------------------- stage wave configs
// Parse Stage_NN.asset -> waves -> entries(statsBase guid, count), resolve guid to SO name.
function parseStage(p) {
  const t = fs.readFileSync(p, 'utf8');
  const entries = [];
  const re = /statsBase:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-f]{32})[^}]*\}[\s\S]{0,200}?count:\s*(\d+)/g;
  let m;
  while ((m = re.exec(t))) {
    const fp = guidToPath[m[1]];
    entries.push({ stats: fp ? path.basename(fp, '.asset') : ('guid:' + m[1]), count: parseInt(m[2], 10) });
  }
  return entries;
}

const SPAWN_DIR = path.join(SO, 'Spawner');
const stages = {};
for (let n = 3; n <= 20; n++) {
  const p = path.join(SPAWN_DIR, 'Stage_' + String(n).padStart(2, '0') + '.asset');
  if (fs.existsSync(p)) stages[n] = parseStage(p);
}
// Stages 1-2 inherit Spawner2.asset from LevelTemplate.prefab (no per-scene override).
const sp2 = path.join(SPAWN_DIR, 'Spawner2.asset');
if (fs.existsSync(sp2)) { stages[1] = parseStage(sp2); stages[2] = parseStage(sp2); }

// ---------------------------------------------------------------- computations
const out = {};

out.configSanity = {
  playerCP_curvesPresent: playerCP._present,
  enemyCP_curvesPresent: enemyCP._present,
  playerProg_curvesPresent: playerProg._present,
  enemyProg_curvesPresent: enemyProg._present,
  // Show what the corrupted curves actually evaluate to.
  playerProg_defPct_at: [1, 2, 10, 25, 50].map(l => +evaluate(playerProg.defPctByLevel, l).toFixed(6)),
  playerProg_rangePct_at: [1, 2, 10, 25, 50].map(l => +evaluate(playerProg.rangePctByLevel, l).toFixed(6)),
  playerCP_meleeMult_raw_at: [1, 10, 50].map(l => +evaluate(playerCP.meleeMultByLevel, l).toFixed(6)),
  playerCP_rangedMult_raw_at: [1, 10, 50].map(l => +evaluate(playerCP.rangedMultByLevel, l).toFixed(6)),
  enemyProg_defPct_at: [1, 2, 10, 50].map(l => +evaluate(enemyProg.defPctByLevel, l).toFixed(6)),
};

// Growth multipliers by level (player + enemy)
out.growthPlayer = [];
out.growthEnemy = [];
for (let L = 1; L <= 50; L++) {
  const gp = getGrowth(L, playerProg), ge = getGrowth(L, enemyProg);
  const r = o => Object.fromEntries(Object.entries(o).map(([k, v]) => [k, +v.toFixed(4)]));
  out.growthPlayer.push({ L, ...r(gp) });
  out.growthEnemy.push({ L, ...r(ge) });
}

// CP weights by level
out.weightsPlayer = [];
out.weightsEnemy = [];
for (let L = 1; L <= 50; L++) {
  const r = o => Object.fromEntries(Object.entries(o).map(([k, v]) => [k, +v.toFixed(6)]));
  out.weightsPlayer.push({ L, ...r(evalWeights(L, playerCP)) });
  out.weightsEnemy.push({ L, ...r(evalWeights(L, enemyCP)) });
}

// --- Player: two growth paths.
//   UI path      (UnitsPanelController): only gA,gH,gMv,gAS applied
//   Combat path  (PlayerStatsApplier)  : all six applied
function grownStats(base, g, allSix) {
  return {
    attack: base.attack * g.gA,
    maxHP: base.maxHP * g.gH,
    moveSpeed: base.moveSpeed * g.gMv,
    attackSpeed: base.attackSpeed * g.gAS,
    defense: base.defense * (allSix ? g.gD : 1),
    attackRange: base.attackRange * (allSix ? g.gR : 1),
    type: base.type,
  };
}

out.heroes = HEROES.map(h => {
  const rows = [];
  for (const L of [1, 2, 3, 5, 10, 15, 20, 30, 50]) {
    const g = getGrowth(L, playerProg);
    const ui = grownStats(h.base, g, false);
    const combat = grownStats(h.base, g, true);
    rows.push({
      L,
      cpUI: unitCP(ui, L, playerCP),
      cpCombat: unitCP(combat, L, playerCP),
      trueDPS: +(ui.attack * ui.attackSpeed).toFixed(2),
      atk: +ui.attack.toFixed(2), hp: +ui.maxHP.toFixed(2),
      defUI: +ui.defense.toFixed(2), defCombat: +combat.defense.toFixed(2),
      atkSpd: +ui.attackSpeed.toFixed(3),
    });
  }
  return { id: h.id, name: h.name, statsAsset: h.stats, base: h.base, rows };
});

// CP composition (share of each term) for a hero, at several levels
function composition(base, L, cfg, prog, allSix) {
  const g = getGrowth(L, prog);
  const s = grownStats(base, g, allSix);
  const w = evalWeights(L, cfg);
  const terms = {
    attack: w.wA * s.attack, maxHP: w.wH * s.maxHP, moveSpeed: w.wMv * s.moveSpeed,
    attackSpeed: w.wAS * s.attackSpeed, defense: w.wD * s.defense, attackRange: w.wR * s.attackRange,
  };
  const total = Object.values(terms).reduce((a, b) => a + b, 0);
  return {
    L, total: +total.toFixed(3), cp: Math.round(total * (RANGED.has(s.type) ? w.rangedMult : w.meleeMult)),
    terms: Object.fromEntries(Object.entries(terms).map(([k, v]) => [k, +v.toFixed(4)])),
    share: Object.fromEntries(Object.entries(terms).map(([k, v]) => [k, +(100 * v / total).toFixed(3)])),
  };
}

const fast = HEROES.find(h => h.id === 1).base;   // atk 64 / atkSpd 2.0
const slow = HEROES.find(h => h.id === 2).base;   // atk 72 / atkSpd 1.5
out.compositionFast = [1, 5, 10, 20, 50].map(L => composition(fast, L, playerCP, playerProg, false));
out.compositionSlow = [1, 5, 10, 20, 50].map(L => composition(slow, L, playerCP, playerProg, false));

// CP vs level curve for both profiles (UI path — what the player actually sees)
out.profileCurves = [];
for (let L = 1; L <= 50; L++) {
  const gf = getGrowth(L, playerProg);
  const sf = grownStats(fast, gf, false), ss = grownStats(slow, gf, false);
  out.profileCurves.push({
    L,
    cpFast: unitCP(sf, L, playerCP), cpSlow: unitCP(ss, L, playerCP),
    dpsFast: +(sf.attack * sf.attackSpeed).toFixed(1),
    dpsSlow: +(ss.attack * ss.attackSpeed).toFixed(1),
  });
}

// --- Enemies: unitLevel == stageLevel; RebuildFromBase applies only gA,gH,gMv,gAS
out.enemies = ENEMIES.map(e => {
  const rows = [];
  for (let S = 1; S <= 20; S++) {
    const g = getGrowth(S, enemyProg);
    const s = grownStats(e.base, g, false);   // gD/gR never applied by EnemyManager
    rows.push({
      S, cp: unitCP(s, S, enemyCP),
      atk: +s.attack.toFixed(1), hp: +s.maxHP.toFixed(1), def: +s.defense.toFixed(1),
    });
  }
  return { name: e.name, base: e.base, rows };
});

// Per-stage total enemy CP (sum over every enemy the stage spawns)
const enemyByName = Object.fromEntries(ENEMIES.map(e => [e.base.name, e]));
out.stageTotals = [];
for (let S = 1; S <= 20; S++) {
  const entries = stages[S] || [];
  const g = getGrowth(S, enemyProg);
  let total = 0, head = 0;
  const breakdown = [];
  for (const en of entries) {
    const e = enemyByName[en.stats];
    if (!e) { breakdown.push({ stats: en.stats, count: en.count, cp: null, note: 'stats asset not in enemy roster' }); continue; }
    const s = grownStats(e.base, g, false);
    const cp = unitCP(s, S, enemyCP);
    total += cp * en.count; head += en.count;
    breakdown.push({ stats: en.stats, count: en.count, cpEach: cp, cpSub: cp * en.count });
  }
  out.stageTotals.push({ S, headcount: head, totalCP: total, breakdown });
}

// --- Formula candidates, scored on the real roster at level 1
// A: current weighted sum.  B: Pokemon-GO-shaped product.  C: DPS x EffectiveHP.
function effHP(s) { return s.maxHP * (100 + Math.max(0, s.defense)) / 100; } // CPCalculator.EffectiveHP
function candB(s) { return Math.floor(s.attack * s.attackSpeed * Math.sqrt(s.defense) * Math.sqrt(s.maxHP) / 10); }
function candC(s) { return Math.round(s.attack * s.attackSpeed * effHP(s) / 100); }
out.candidates = HEROES.map(h => {
  const s = h.base;
  return {
    id: h.id, name: h.name,
    trueDPS: +(s.attack * s.attackSpeed).toFixed(1),
    effHP: +effHP(s).toFixed(1),
    A_current: unitCP(s, 1, playerCP),
    B_product: candB(s),
    C_dpsEhp: candC(s),
  };
});

// Same three, on the enemy roster at stage 1
out.candidatesEnemy = ENEMIES.map(e => {
  const s = e.base;
  return {
    name: e.name,
    trueDPS: +(s.attack * s.attackSpeed).toFixed(1),
    effHP: +effHP(s).toFixed(1),
    A_current: unitCP(s, 1, enemyCP),
    B_product: candB(s),
    C_dpsEhp: candC(s),
  };
});

const dest = path.join(__dirname, 'out.json');
fs.writeFileSync(dest, JSON.stringify(out, null, 2));

// ---------------------------------------------------------------- sanity check vs plan
const g20 = getGrowth(20, playerProg);
const checks = [
  ['hero id1 (fast) CP @L1 == 81', out.heroes.find(h => h.id === 1).rows[0].cpUI, 81],
  ['hero id2 (slow) CP @L1 == 89', out.heroes.find(h => h.id === 2).rows[0].cpUI, 89],
  ['player gA @L20 ~= 3.6025', +g20.gA.toFixed(4), 3.6025],
  ['player gH @L20 ~= 4.9424', +g20.gH.toFixed(4), 4.9424],
  ['player gD @L20 ~= 3.1650', +g20.gD.toFixed(4), 3.1650],
  ['player gR @L20 ~= 3.4569', +g20.gR.toFixed(4), 3.4569],
  ['Golem_02 CP @stage20 == 478', out.enemies.find(e => e.name === 'Enemy_Golem_02').rows[19].cp, 478],
];
console.log('--- SANITY CHECKS (script vs hand-computed plan values) ---');
let bad = 0;
for (const [label, got, want] of checks) {
  const ok = Math.abs(got - want) <= (Number.isInteger(want) && Math.abs(want) > 10 ? 1 : 0.0002);
  if (!ok) bad++;
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${label}   got=${got} want=${want}`);
}
console.log(bad ? `\n${bad} CHECK(S) FAILED` : '\nall checks passed');
console.log('\n--- config sanity ---');
console.log(JSON.stringify(out.configSanity, null, 2));
console.log('\n--- stage wave parse (headcounts) ---');
console.log(out.stageTotals.map(s => `S${s.S}: n=${s.headcount} totalCP=${s.totalCP}`).join('\n'));
console.log('\nwrote ' + dest);
