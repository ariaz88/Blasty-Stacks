// build.js — injects the computed dataset into the report template.
const fs = require('fs'), path = require('path');
const dir = __dirname;
const out = JSON.parse(fs.readFileSync(path.join(dir, 'out.json'), 'utf8'));
const r = (v, d) => +Number(v).toFixed(d);

const DATA = {
  growthPlayer: out.growthPlayer.map(g => ({
    L: g.L, gA: r(g.gA, 4), gH: r(g.gH, 4), gMv: r(g.gMv, 4),
    gAS: r(g.gAS, 4), gD: r(g.gD, 4), gR: r(g.gR, 4),
  })),
  profileCurves: out.profileCurves.map(p => ({
    L: p.L, cpFast: p.cpFast, cpSlow: p.cpSlow,
    dpsFast: r(p.dpsFast, 1), dpsSlow: r(p.dpsSlow, 1),
  })),
  compositionFast: out.compositionFast.map(c => ({ L: c.L, cp: c.cp, share: c.share })),
  heroes: out.heroes.map(h => ({
    id: h.id, name: h.name,
    base: {
      attack: h.base.attack, defense: h.base.defense, maxHP: h.base.maxHP,
      attackSpeed: h.base.attackSpeed, moveSpeed: h.base.moveSpeed,
      attackRange: h.base.attackRange, type: h.base.type,
    },
    rows: h.rows.map(x => ({ L: x.L, cpUI: x.cpUI, trueDPS: x.trueDPS })),
  })),
  enemies: out.enemies.map(e => ({
    name: e.name,
    base: {
      attack: e.base.attack, defense: e.base.defense, maxHP: e.base.maxHP,
      attackSpeed: e.base.attackSpeed, moveSpeed: e.base.moveSpeed,
    },
    rows: e.rows.map(x => ({ S: x.S, cp: x.cp })),
  })),
  stageTotals: out.stageTotals.map(s => ({
    S: s.S, headcount: s.headcount, totalCP: s.totalCP,
    breakdown: s.breakdown.map(b => ({ stats: b.stats, count: b.count })),
  })),
  candidatesEnemy: out.candidatesEnemy,
};

const tpl = fs.readFileSync(path.join(dir, 'report.template.html'), 'utf8');
const marker = '/*__DATA__*/ null';
if (!tpl.includes(marker)) { console.error('FAIL: data marker not found in template'); process.exit(1); }
const html = tpl.replace(marker, JSON.stringify(DATA));

const reportDir = path.resolve(dir, '..', 'report');
fs.mkdirSync(reportDir, { recursive: true });
const dest = path.join(reportDir, 'CP_System_Report.html');
fs.writeFileSync(dest, html);

// sanity: the injected values the page will actually render
const h1 = DATA.heroes.find(h => h.id === 1), h2 = DATA.heroes.find(h => h.id === 2);
console.log('injected bytes :', html.length.toLocaleString());
console.log('hero rows      :', h1.rows.map(x => x.L).join(','));
console.log('id1 CP L1/L10/L20 :', h1.rows[0].cpUI, h1.rows[4].cpUI, h1.rows[6].cpUI);
console.log('id2 CP L1/L10/L20 :', h2.rows[0].cpUI, h2.rows[4].cpUI, h2.rows[6].cpUI);
console.log('enemy S idx 0/4/9/14/19 :', DATA.enemies[0].rows[0].S, DATA.enemies[0].rows[4].S,
            DATA.enemies[0].rows[9].S, DATA.enemies[0].rows[14].S, DATA.enemies[0].rows[19].S);
console.log('stages         :', DATA.stageTotals.length, 'first/last CP',
            DATA.stageTotals[0].totalCP, DATA.stageTotals[19].totalCP);
console.log('growth rows    :', DATA.growthPlayer.length, '| profile rows', DATA.profileCurves.length);
console.log('wrote          :', dest);
