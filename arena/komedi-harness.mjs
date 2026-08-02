// MEŞRUİYET: ARENA — comedy engine, console harness.
// Profile -> tech tier -> four army slots -> dominant cause -> Turkish flavour line.
// No Unity, no graphics. The point is to read 200 outputs and decide whether the joke survives.

// ---------------------------------------------------------------- deterministic rng
let S = 1337;
const rnd = () => (S = (S * 1103515245 + 12345) & 0x7fffffff) / 0x7fffffff;
const ri = (a, b) => a + Math.floor(rnd() * (b - a + 1));
const pick = a => a[ri(0, a.length - 1)];

// ---------------------------------------------------------------- turkish grammar
const VOWELS = 'aeıioöuüAEIİOÖUÜ';
const lastVowel = w => {
  for (let i = w.length - 1; i >= 0; i--) if (VOWELS.includes(w[i])) return w[i].toLowerCase();
  return 'a';
};
// genitive: X'in / X'ın / X'un / X'ün, with buffer n after a vowel
function genitive(name) {
  const v = lastVowel(name);
  const suf = 'aı'.includes(v) ? 'ın' : 'ei'.includes(v) ? 'in' : 'ou'.includes(v) ? 'un' : 'ün';
  const endsVowel = VOWELS.includes(name[name.length - 1]);
  return `${name}'${endsVowel ? 'n' : ''}${suf}`;
}
// "da/de" clitic — separate word, harmonised to the last vowel
function clitic(word) {
  return 'aıou'.includes(lastVowel(word)) ? 'da' : 'de';
}
// "ile" -> "yla/yle" after vowel, "la/le" after consonant
function withSuffix(word) {
  const v = lastVowel(word);
  const back = 'aıou'.includes(v);
  const endsVowel = VOWELS.includes(word[word.length - 1]);
  return word + (endsVowel ? 'y' : '') + (back ? 'la' : 'le');
}

// ---------------------------------------------------------------- tech tier
function techTier(p) {
  const seats = p.egitim;
  let bina = 0;
  if (p.bina.has('MEKTEP')) bina = 1;
  if (p.bina.has('DOKUMHANE')) bina = 2;
  if (p.bina.has('UNIVERSITE')) bina = 3;
  if (p.bina.has('UNIVERSITE') && p.bina.has('ARASTIRMA')) bina = 4;
  const egitimTier = seats >= 70 ? 4 : seats >= 50 ? 3 : seats >= 30 ? 2 : seats >= 15 ? 1 : 0;
  return Math.max(0, Math.min(bina, egitimTier));
}

// ---------------------------------------------------------------- archetypes
// gate: hard conditions. score: soft points. role: slot.
const A = [
  // ---- ÖNCÜ
  { id:'TAS_ATAN_GENCLER', ad:'Taş Atan Gençler', rol:'ÖNCÜ', atk:4, def:2,
    gate:p=>p.egitim<35 && p.nufus>=800, score:p=>(35-p.egitim)*3 + 40,
    sebep:'EGITIM_YOK' },
  { id:'FANATIK_TARAFTAR', ad:'Fanatik Taraftar Grubu', rol:'ÖNCÜ', atk:7, def:3,
    gate:p=>p.moral>=75, score:p=>(p.moral-75)*4 + 60, sebep:'MORAL_YUKSEK' },
  { id:'KREDI_KARTLI_SUVARI', ad:'Kredi Kartlı Süvari', rol:'ÖNCÜ', atk:9, def:5,
    gate:p=>p.ekonomi>=45 && p.ekonomi<=75 && p.borc>0, score:p=>70+p.borc/20,
    sebep:'BORC' },
  { id:'MOTOKURYE', ad:'Motokurye Akıncıları', rol:'ÖNCÜ', atk:8, def:3,
    gate:p=>p.teknoloji>=40 && p.bina.has('KARGO'), score:p=>p.teknoloji, sebep:'TEKNOLOJI' },
  { id:'ADAK_KOCU', ad:'Adak Koçu Sürüsü', rol:'ÖNCÜ', atk:6, def:4,
    gate:p=>p.din>=65 && p.ekonomi<45, score:p=>(p.din-65)*3+50, sebep:'DIN' },
  { id:'EMEKLI_TUGAY', ad:'Emekli Taarruz Tugayı', rol:'ÖNCÜ', atk:5, def:6,
    gate:p=>p.nufus>=2500 && p.egitim<55, score:p=>p.nufus/60, sebep:'NUFUS_COK' },
  { id:'ZOO_FIRARI', ad:'Hayvanat Bahçesi Firarileri', rol:'ÖNCÜ', atk:11, def:2,
    gate:p=>p.yikilan.has('HAYVANAT'), score:()=>9500, sebep:'ZOO_YIKILDI' },
  { id:'TEK_KISILIK', ad:'Tek Kişilik Ordu', rol:'ÖNCÜ', atk:14, def:9,
    gate:p=>p.nufus<400, score:p=>(400-p.nufus)/4+60, sebep:'NUFUS_YOK' },
  { id:'KOSAN_KALABALIK', ad:'Koşan Kalabalık', rol:'ÖNCÜ', atk:3, def:2,
    gate:()=>true, score:()=>10, sebep:'YOK' },

  // ---- HAT
  { id:'MANCINIK', ad:'Mancınık Müfrezesi', rol:'HAT', atk:6, def:5,
    gate:p=>!p.bina.has('UNIVERSITE') && p.tier<=1, score:p=>200-p.egitim*2, sebep:'UNI_YOK' },
  { id:'TENCERE_ZIRHLI', ad:'Tencere Zırhlılar', rol:'HAT', atk:8, def:10,
    gate:p=>p.tier<=1 && p.ekonomi<40, score:p=>90-p.ekonomi, sebep:'FAKIR' },
  { id:'SENDIKA_KOLU', ad:'Sendika Kolu', rol:'HAT', atk:9, def:8,
    gate:p=>p.bina.has('FABRIKA') && p.moral<50, score:p=>(50-p.moral)*3+60, sebep:'ISCI_KIZGIN' },
  { id:'PARALI_ASKER', ad:'Paralı Askerler', rol:'HAT', atk:13, def:9,
    gate:p=>p.ekonomi>=80, score:p=>p.ekonomi*1.5, sebep:'PARA_VAR', not:'sadakat belirsiz' },
  { id:'ZIRHLI_TUGAY', ad:'Zırhlı Tugay', rol:'HAT', atk:16, def:14,
    gate:p=>p.tier>=3 && p.bina.has('DOKUMHANE'), score:p=>p.tier*40+p.teknoloji, sebep:'SANAYI' },
  { id:'JANDARMA', ad:'Jandarma Taburu', rol:'HAT', atk:11, def:12,
    gate:p=>p.bina.has('KISLA'), score:p=>80+p.guvenlik, sebep:'KISLA' },
  { id:'ACLIK_ALAYI', ad:'Açlık Alayı', rol:'HAT', atk:7, def:4,
    gate:p=>p.yiyecek<25, score:p=>(25-p.yiyecek)*6+70, sebep:'ACLIK' },
  { id:'KOYLU_MILISI', ad:'Köylü Milisi', rol:'HAT', atk:5, def:6,
    gate:()=>true, score:()=>12, sebep:'YOK' },

  // ---- DESTEK
  { id:'AGITCILAR', ad:'Ağıtçılar', rol:'DESTEK', atk:0, def:3,
    gate:p=>p.moral<40, score:p=>(40-p.moral)*4+40, sebep:'MORAL_DUSUK' },
  { id:'SAPANCILAR', ad:'Sapancılar', rol:'DESTEK', atk:4, def:2,
    gate:p=>p.tier<=1, score:p=>60-p.teknoloji, sebep:'TEKNOLOJI_YOK' },
  { id:'BANDO', ad:'Belediye Bandosu', rol:'DESTEK', atk:1, def:2,
    gate:p=>p.kultur>=55, score:p=>p.kultur, sebep:'KULTUR' },
  { id:'TOPCU', ad:'Topçu Bataryası', rol:'DESTEK', atk:15, def:4,
    gate:p=>p.tier>=2, score:p=>p.tier*35, sebep:'SANAYI' },
  { id:'IHA_PILOTSUZ', ad:'İHA Filosu (Pilotsuz)', rol:'DESTEK', atk:3, def:1,
    gate:p=>p.tier>=4 && p.nufus<600, score:()=>400, sebep:'ADAM_YOK' },
  { id:'IHA', ad:'İHA Filosu', rol:'DESTEK', atk:22, def:3,
    gate:p=>p.tier>=4 && p.nufus>=600, score:p=>300+p.teknoloji, sebep:'ILERI_TEK' },
  { id:'DUACILAR', ad:'Duacılar Heyeti', rol:'DESTEK', atk:0, def:2,
    gate:p=>p.din>=70, score:p=>p.din*1.2, sebep:'DIN' },
  { id:'MEGAFONCU', ad:'Megafonlu Muhtar', rol:'DESTEK', atk:1, def:1,
    gate:()=>true, score:()=>8, sebep:'YOK' },

  // ---- ÖZEL
  { id:'OGRENCI_KONSEYI', ad:'Öğrenci Konseyi', rol:'ÖZEL', atk:6, def:4,
    gate:p=>p.bina.has('UNIVERSITE') && p.moral<45, score:p=>(45-p.moral)*5+80,
    sebep:'OGRENCI_KIZGIN' },
  { id:'ITFAIYE', ad:'İtfaiye Bölüğü', rol:'ÖZEL', atk:2, def:12,
    gate:p=>p.yikilan.size>=2, score:p=>p.yikilan.size*70, sebep:'YIKIM' },
  { id:'KACAKCILAR', ad:'Kaçakçılar Loncası', rol:'ÖZEL', atk:9, def:5,
    gate:p=>p.ekonomi>=60 && p.guvenlik<40, score:p=>p.ekonomi-p.guvenlik, sebep:'ASAYIS_YOK' },
  { id:'GREVCILER', ad:'Grevdeki İşçiler', rol:'ÖZEL', atk:0, def:0,
    gate:p=>p.moral<25 && p.bina.has('FABRIKA'), score:p=>(25-p.moral)*10+120,
    sebep:'GREV', not:'savaşmayı reddediyor' },
  { id:'MUHENDISLER', ad:'Mühendisler Takımı', rol:'ÖZEL', atk:4, def:6,
    gate:p=>p.tier>=3, score:p=>p.tier*25, sebep:'MUHENDIS' },
  { id:'KIRLILIK_MASKESI', ad:'Maskeli Sanayi Bölüğü', rol:'ÖZEL', atk:8, def:7,
    gate:p=>p.kirlilik>=60, score:p=>p.kirlilik, sebep:'KIRLILIK' },
  { id:'MAHALLE_BEKCISI', ad:'Mahalle Bekçisi', rol:'ÖZEL', atk:3, def:3,
    gate:()=>true, score:()=>6, sebep:'YOK' },
];

// ---------------------------------------------------------------- cause fragments
// alt: subordinate clause ending so "için" can follow.  son: how the army shows up.
const SEBEP = {
  EGITIM_YOK: { alt:['okulu olmadığı','tek öğretmeni de göç ettiği','okuma yazma oranı utanç verici olduğu'], agir:9 },
  UNI_YOK: { alt:['üniversitesi olmadığı','mühendis diye kimsesi olmadığı','en yüksek diplomanın lise olduğu'], agir:10 },
  MORAL_YUKSEK: { alt:['halkı fazla coştuğu','şehirde kimse sonucu düşünmediği','maç galibiyeti kutlandığı'], agir:6 },
  MORAL_DUSUK: { alt:['kimse savaşmak istemediği','halkın morali dipte olduğu','herkes küskün olduğu'], agir:8 },
  BORC: { alt:['bütün parası faize gittiği','hazinesi alacaklılara emanet olduğu','borcu bütçesini aştığı'], agir:8 },
  TEKNOLOJI: { alt:['elinde bir tek motosikletler kaldığı','sanayisi yarım kaldığı','tek modern aracı kurye filosu olduğu'], agir:6 },
  TEKNOLOJI_YOK: { alt:['teknolojiden anlamadığı','en gelişmiş aleti sapan olduğu','demir dökemediği'], agir:7 },
  DIN: { alt:['duayla halledileceğine inandığı','kurban kesmenin yeteceğini düşündüğü','hocaya danışıp karar verdiği'], agir:7 },
  NUFUS_COK: { alt:['genç bulamadığı','nüfusun yarısı emekli olduğu','kalabalık ama yaşlı olduğu'], agir:7 },
  NUFUS_YOK: { alt:['şehirde başka kimse kalmadığı','nüfusu üç haneye düştüğü','herkes çoktan göç ettiği'], agir:11 },
  ZOO_YIKILDI: { alt:['hayvanat bahçesi vurulduğu','kafesler açık kaldığı','aslanlar sokağa çıktığı'], agir:12 },
  FAKIR: { alt:['zırh alacak parası olmadığı','demir yerine mutfak eşyası kullandığı','bütçesi tencereye ancak yettiği'], agir:8 },
  ISCI_KIZGIN: { alt:['işçileri zaten kızgın olduğu','fabrikada üç aydır maaş ödenmediği','sendika sabrı tükendiği'], agir:7 },
  PARA_VAR: { alt:['parayla her şeyin çözüleceğini sandığı','askerini ihaleyle aldığı','ordusunu peşin ödeyip kiraladığı'], agir:8 },
  SANAYI: { alt:['fabrikaları tam kapasite çalıştığı','dökümhanesi gece gündüz döktüğü','sanayisi nihayet oturduğu'], agir:5 },
  KISLA: { alt:['kışlası olduğu','tek düzgün kurumu ordu olduğu','garnizonu hazır beklediği'], agir:4 },
  ACLIK: { alt:['ambarı boş olduğu','üç turdur ekmek çıkmadığı','değirmeni tıkalı olduğu'], agir:10 },
  KULTUR: { alt:['bandodan başka kurumu kalmadığı','kültür bütçesi orduyu geçtiği','tek yatırımı konser salonu olduğu'], agir:6 },
  ADAM_YOK: { alt:['uçuracak pilot bulamadığı','teknoloji alıp insan bulamadığı','cihazı var operatörü yok olduğu'], agir:11 },
  ILERI_TEK: { alt:['araştırma merkezi tamamlandığı','laboratuvarları sonuç verdiği','teknolojide kimseyi dinlemediği'], agir:5 },
  OGRENCI_KIZGIN: { alt:['öğrenciler ayaklandığı','kampüs işgal edildiği','yurtlar boşaltılmadığı'], agir:9 },
  YIKIM: { alt:['şehrin yarısı yandığı','moloz kaldırılamadığı','itfaiye hâlâ sahada olduğu'], agir:10 },
  ASAYIS_YOK: { alt:['asayiş kalmadığı','limanı kaçakçıya emanet olduğu','polisin adı kaldığı'], agir:7 },
  GREV: { alt:['herkes grevde olduğu','fabrikalar durduğu','işçiler cepheye gitmeyi reddettiği'], agir:11 },
  MUHENDIS: { alt:['mühendislerini cepheye sürdüğü','laboratuvarı boşaltıp sıraya dizdiği','teknik kadrosunu silahlandırdığı'], agir:5 },
  KIRLILIK: { alt:['havası solunamadığı','askerleri öksürdüğü','bacaları hiç durmadığı'], agir:7 },
  YOK: { alt:['başka çaresi kalmadığı','eldeki tek seçenek bu olduğu','kimseye danışmadığı'], agir:1 },
};

// ---------------------------------------------------------------- generate
function army(p) {
  p.tier = techTier(p);
  const slots = {};
  for (const rol of ['ÖNCÜ', 'HAT', 'DESTEK', 'ÖZEL']) {
    const cands = A.filter(a => a.rol === rol && a.gate(p));
    cands.sort((a, b) => b.score(p) - a.score(p));
    slots[rol] = cands[0];
  }
  const seferberlik = Math.round(p.nufus * (0.04 + 0.04 * p.moral / 100));
  const pay = { 'ÖNCÜ': 0.20, 'HAT': 0.45, 'DESTEK': 0.25, 'ÖZEL': 0.10 };
  const teknoCarp = [1.0, 1.5, 2.4, 3.8, 6.0][p.tier];
  const moralCarp = 0.6 + 0.008 * p.moral;

  const cards = Object.entries(slots).map(([rol, a]) => {
    const adet = Math.max(1, Math.round(seferberlik * pay[rol] / 25));
    const guc = Math.round(adet * (a.atk + a.def) / 2 * moralCarp * teknoCarp);
    return { rol, a, adet, guc };
  });

  // dominant cause = heaviest weight among the four, ties broken by slot power
  const toplam = Math.max(1, cards.reduce((s, c) => s + c.guc, 0));
  // Weight by the unit's SHARE OF REAL POWER, so the headline never sells a 2-power
  // support squad as the story while the line infantry does the fighting.
  const scored = cards.map(c => ({
    c, w: (SEBEP[c.a.sebep]?.agir ?? 0) * (0.25 + 1.75 * (c.guc / toplam)),
  })).sort((x, y) => y.w - x.w);
  const best = scored[0].c;
  const second = scored.find(x => x.c.a.sebep !== best.a.sebep && x.w > 2.2)?.c ?? null;
  return { cards, best, second, toplam, tier: p.tier };
}

const KALIP = [
  (n,s,b,g) => `${genitive(n)} ${s} için savaşa ${withSuffix(b)} katılacak.`,
  (n,s,b,g) => `${n} cepheye ${withSuffix(b)} gidiyor; ${s} söyleniyor.`,
  (n,s,b,g) => `${genitive(n)} ordusu ${b}. Sebep: ${s} için başka bir şey bulunamamış.`,
  (n,s,b,g) => `${b} yola çıktı. ${genitive(n)} ${s} rivayet ediliyor.`,
  (n,s,b,g) => `${n} savaşa gidiyor. Yanında ${b}, arkasında ${s} gerçeği.`,
  (n,s,b,g) => `${genitive(n)} en güçlü birliği: ${b}. Çünkü ${s} biliniyor.`,
  (n,s,b,g) => `Cepheye ${b} sürüldü — ${genitive(n)} ${s} herkesin malumu.`,
  (n,s,b,g) => `${n} ${withSuffix(b)} saldırıyor; ${s} kimsenin umurunda değil.`,
  (n,s,b,g) => `${genitive(n)} sancağı altında ${b} toplandı; ${s} kimse hatırlatmadı.`,
  (n,s,b,g) => `${b} sınırda bekliyor. ${genitive(n)} ${s} zaten biliniyordu.`,
];

function line(name, p, r) {
  const s = SEBEP[r.best.a.sebep];
  const alt = pick(s.alt);
  const birlik = r.best.a.ad;
  const not = r.best.a.not ? ` (${r.best.a.not})` : '';
  // Template chosen from state, not at random, so the same city tells the same joke.
  const idx = (r.tier * 3 + r.cards.length + Math.round(r.toplam / 7) + name.length) % KALIP.length;
  let out = KALIP[idx](name, alt, birlik, r.toplam);
  if (r.second && (r.toplam + name.length) % 3 === 0) {
    const s2 = SEBEP[r.second.a.sebep];
    out += ` Ayrıca ${pick(s2.alt)} için ${r.second.a.ad} ${clitic(r.second.a.ad)} yanlarında.`;
  }
  return out + not;
}

// ---------------------------------------------------------------- random profiles
const ADLAR = ['İsmail','Ayşe','Kaan','Zeynep','Ömer','Burak','Elif','Mert','Deniz','Hakan',
               'Sinem','Tuna','Emre','Gökçe','Barış','Ceren','Onur','Pelin'];
const BINALAR = ['MEKTEP','UNIVERSITE','ARASTIRMA','DOKUMHANE','FABRIKA','KISLA','KARGO','HAYVANAT'];

function randomProfile() {
  const bina = new Set(BINALAR.filter(() => rnd() < 0.38));
  const yikilan = new Set([...bina].filter(() => rnd() < 0.18));
  for (const y of yikilan) bina.delete(y);
  return {
    egitim: ri(0, 100), ekonomi: ri(0, 100), teknoloji: ri(0, 100),
    moral: ri(5, 100), kirlilik: ri(0, 100), din: ri(0, 100),
    guvenlik: ri(0, 100), kultur: ri(0, 100), yiyecek: ri(0, 100),
    nufus: ri(150, 4000), borc: rnd() < 0.4 ? ri(100, 900) : 0,
    bina, yikilan,
  };
}

// ---------------------------------------------------------------- run
const N = parseInt(process.argv[2] || '200', 10);
const lines = [], causeCount = {}, unitCount = {};
for (let i = 0; i < N; i++) {
  const name = pick(ADLAR);
  const p = randomProfile();
  const r = army(p);
  const l = line(name, p, r);
  lines.push({ l, r, p, name });
  causeCount[r.best.a.sebep] = (causeCount[r.best.a.sebep] || 0) + 1;
  for (const c of r.cards) unitCount[c.a.id] = (unitCount[c.a.id] || 0) + 1;
}

if (process.argv[3] === '--stats') {
  const uniq = new Set(lines.map(x => x.l.replace(/^[^ ]+ /, ''))).size;
  console.log(`ÜRETİLEN: ${N}   BENZERSİZ CÜMLE GÖVDESİ: ${uniq}  (%${(100*uniq/N).toFixed(0)})`);
  console.log('\nBASKIN SEBEP DAĞILIMI (ilk 12):');
  Object.entries(causeCount).sort((a,b)=>b[1]-a[1]).slice(0,12)
    .forEach(([k,v]) => console.log(`   ${String(v).padStart(3)}  ${(100*v/N).toFixed(1).padStart(5)}%  ${k}`));
  const never = A.filter(a => !unitCount[a.id]);
  console.log(`\nHİÇ SEÇİLMEYEN BİRİM: ${never.length ? never.map(a=>a.ad).join(', ') : 'yok'}`);
  const top = Object.entries(unitCount).sort((a,b)=>b[1]-a[1])[0];
  console.log(`EN SIK BİRİM: ${A.find(a=>a.id===top[0]).ad} — ${N*4} slotun %${(100*top[1]/(N*4)).toFixed(1)}'i`);
} else {
  const n = parseInt(process.argv[3] || '30', 10);
  lines.slice(0, n).forEach(({ l, r }, i) => {
    console.log(`\n${String(i+1).padStart(3)}. ${l}`);
    console.log(`     T${r.tier} · toplam güç ${r.toplam}`);
    console.log('     ' + r.cards.map(c => `[${c.a.sebep}] ${c.a.ad} ×${c.adet} (${c.guc})`).join('  '));
  });
}
