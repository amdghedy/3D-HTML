using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace ClearView.Export
{
    public static class PackageBuilder
    {
        public static void Write(ExportSettings settings, RevitExportContext context)
        {
            var gltf = GltfWriter.Build(context.Meshes);
            var template = settings.EnableViewerTools ? ViewerWithTools : ViewerBasic;
            var html = template
                .Replace("{{PROJECT_NAME}}", Esc(settings.ProjectName))
                .Replace("{{PROJECT_NUMBER}}", Esc(settings.ProjectNumber))
                .Replace("{{VIEW_NAME}}", Esc(settings.ViewName))
                .Replace("{{BRAND}}", Esc(settings.Brand))
                .Replace("{{GLTF_JSON}}", gltf.ToString(Formatting.None));

            var dir = Path.GetDirectoryName(settings.OutputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(settings.OutputPath, html);
        }

        static string Esc(string s) =>
            (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                     .Replace("\"", "&quot;");

        const string ViewerBasic = @"<!DOCTYPE html>
<html lang='en'><head><meta charset='utf-8'/><meta name='viewport' content='width=device-width, initial-scale=1'/>
<title>{{PROJECT_NAME}} — 3D HTML Viewer</title>
<style>
:root{--bg:#ffffff;--fg:#1a1a1a;--muted:#666;--surface:#f5f5f5;--border:#ddd}
html,body{margin:0;height:100%;background:var(--bg);color:var(--fg);font-family:system-ui,sans-serif}
canvas{position:fixed;inset:0}#ui{position:fixed;top:16px;left:16px;width:min(280px,calc(100vw - 32px));max-height:calc(100vh - 32px);overflow:auto;background:#fff;border:1px solid var(--border);border-radius:12px;padding:16px;z-index:2;box-shadow:0 4px 20px rgba(0,0,0,.08)}
h1{font-size:18px;margin:4px 0 0}p,label{font-size:13px;color:var(--muted)}label{display:flex;gap:8px;align-items:center;min-height:36px}
h2{font-size:11px;letter-spacing:.08em;text-transform:uppercase;color:var(--muted);margin:14px 0 6px}
</style></head><body>
<canvas id='c'></canvas><aside id='ui'><p>3D HTML</p><h1>{{PROJECT_NAME}}</h1><p>{{VIEW_NAME}} · {{PROJECT_NUMBER}}</p><div id='layers'></div></aside>
<script type='importmap'>{""imports"":{""three"":""https://unpkg.com/three@0.170.0/build/three.module.js"",""three/addons/"":""https://unpkg.com/three@0.170.0/examples/jsm/""}}</script>
<script type='module'>
import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
const GLTF = {{GLTF_JSON}};
const renderer = new THREE.WebGLRenderer({ canvas: document.getElementById('c'), antialias: true });
renderer.setClearColor(0xf0f0f0, 1);
renderer.setPixelRatio(Math.min(devicePixelRatio, 1.75));
const scene = new THREE.Scene(); scene.background = new THREE.Color(0xf0f0f0);
const camera = new THREE.PerspectiveCamera(42, 1, 0.1, 2000); camera.position.set(40, 30, 40);
const controls = new OrbitControls(camera, renderer.domElement); controls.enableDamping = true;
scene.add(new THREE.AmbientLight(0xffffff, 0.7));
scene.add(new THREE.HemisphereLight(0xffffff, 0xbbbbbb, 0.5));
const sun = new THREE.DirectionalLight(0xffffff, 0.7); sun.position.set(20, 30, 10); scene.add(sun);
new GLTFLoader().parse(JSON.stringify(GLTF), '', (g) => {
  scene.add(g.scene);
  const box = new THREE.Box3().setFromObject(g.scene);
  const c = box.getCenter(new THREE.Vector3()); const s = box.getSize(new THREE.Vector3());
  const span = Math.max(s.x,s.y,s.z,1);
  controls.target.copy(c); camera.position.copy(c).add(new THREE.Vector3(span, span*0.7, span));
  const byLevel={}, byCat={};
  g.scene.traverse(o => { const ex=o.userData; if(!ex) return;
    if(ex.level) (byLevel[ex.level]||=[]).push(o); if(ex.category) (byCat[ex.category]||=[]).push(o); });
  const boxEl=document.getElementById('layers');
  function group(title,map){ const h=document.createElement('h2'); h.textContent=title; boxEl.appendChild(h);
    Object.keys(map).sort().forEach(k=>{ const lab=document.createElement('label'); const cb=document.createElement('input');
      cb.type='checkbox'; cb.checked=true; cb.onchange=()=>map[k].forEach(o=>o.visible=cb.checked);
      lab.appendChild(cb); lab.appendChild(document.createTextNode(' '+k)); boxEl.appendChild(lab); }); }
  group('Levels', byLevel); group('Categories', byCat);
});
function resize(){ const w=innerWidth,h=innerHeight; renderer.setSize(w,h,false); camera.aspect=w/h; camera.updateProjectionMatrix(); }
addEventListener('resize', resize); resize();
(function loop(){ controls.update(); renderer.render(scene, camera); requestAnimationFrame(loop); })();
</script></body></html>";

        const string ViewerWithTools = @"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""utf-8""/>
<meta name=""viewport"" content=""width=device-width, initial-scale=1""/>
<meta name=""color-scheme"" content=""dark""/>
<title>{{PROJECT_NAME}} — 3D HTML Viewer</title>
<style>
:root{
  --bg:#2a2d32;
  --viewport:#3c4046;
  --surface:#1e2024;
  --surface-2:#2a2d32;
  --border:#3a3e45;
  --border-strong:#4a4f58;
  --text:#e8eaed;
  --muted:#9aa0a6;
  --accent:#0696d7;
  --accent-hover:#28a8e0;
  --danger:#f28b82;
  --shadow:0 8px 32px rgba(0,0,0,.45);
  --radius:8px;
  --font:""Artifakt Element"",""Segoe UI"",system-ui,-apple-system,sans-serif;
  --mono:ui-monospace,Consolas,monospace;
  --bar-h:48px;
  --bottom-h:56px;
}
*{box-sizing:border-box}
html,body{margin:0;height:100%;background:var(--bg);color:var(--text);font-family:var(--font);overflow:hidden;-webkit-font-smoothing:antialiased}
canvas#c{position:fixed;inset:0;display:block}

/* ---- Top bar ---- */
#topbar{
  position:fixed;top:0;left:0;right:0;height:var(--bar-h);z-index:40;
  display:flex;align-items:center;gap:8px;padding:0 14px;
  background:#1a1c1f;border-bottom:1px solid var(--border);
}
#topbar .brand{font-size:13px;font-weight:600;color:var(--text);letter-spacing:.02em;margin-right:4px}
#topbar .sep{color:var(--muted);margin:0 4px;opacity:.5}
#topbar .doc{font-size:13px;color:var(--muted);max-width:280px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
#topbar .spacer{flex:1}
#topbar button{
  height:32px;padding:0 12px;border:1px solid transparent;border-radius:6px;
  background:transparent;color:var(--text);cursor:pointer;font-size:12.5px;font-weight:500;
  font-family:inherit;transition:background .12s,border-color .12s;
  display:inline-flex;align-items:center;gap:6px;
}
#topbar button:hover{background:rgba(255,255,255,.06);border-color:var(--border)}
#topbar button.active{background:rgba(6,150,215,.18);color:var(--accent);border-color:rgba(6,150,215,.35)}

/* ---- Bottom toolbar (Autodesk-style) ---- */
#bottombar{
  position:fixed;left:50%;bottom:14px;transform:translateX(-50%);z-index:40;
  display:flex;align-items:center;gap:2px;padding:6px 8px;
  background:rgba(30,32,36,.94);backdrop-filter:blur(12px);-webkit-backdrop-filter:blur(12px);
  border:1px solid var(--border);border-radius:12px;box-shadow:var(--shadow);
}
#bottombar button{
  width:42px;height:42px;border:none;border-radius:8px;background:transparent;
  color:var(--muted);cursor:pointer;font-size:11px;font-weight:500;font-family:inherit;
  display:flex;flex-direction:column;align-items:center;justify-content:center;gap:2px;
  transition:background .12s,color .12s;
}
#bottombar button:hover{background:rgba(255,255,255,.08);color:var(--text)}
#bottombar button.active{background:rgba(6,150,215,.2);color:var(--accent)}
#bottombar button .ico{font-size:16px;line-height:1;opacity:.95}
#bottombar .vdiv{width:1px;height:28px;background:var(--border);margin:0 4px}
#bottombar button.wide{width:auto;padding:0 12px;flex-direction:row;gap:6px;font-size:12px}
#bottombar button.wide .ico{font-size:14px}

/* ---- Side panels ---- */
.panel{
  position:fixed;top:calc(var(--bar-h) + 12px);bottom:84px;width:min(300px,90vw);z-index:30;
  background:var(--surface);border:1px solid var(--border);border-radius:var(--radius);
  display:flex;flex-direction:column;box-shadow:var(--shadow);animation:fadeIn .18s ease;
}
@keyframes fadeIn{from{opacity:0;transform:translateY(4px)}to{opacity:1;transform:none}}
.panel.left{left:12px}
.panel.right{right:12px}
.panel.closed{display:none}
.panel.expanded{width:min(420px,94vw)}
.panel-head{
  display:flex;align-items:center;gap:8px;padding:12px 14px;
  border-bottom:1px solid var(--border);background:#181a1d;border-radius:var(--radius) var(--radius) 0 0;
}
.panel-head h2{margin:0;font-size:13px;font-weight:600;flex:1;color:var(--text)}
.panel-head button{
  border:1px solid var(--border);background:transparent;border-radius:6px;
  padding:4px 10px;cursor:pointer;font-size:12px;color:var(--muted);font-family:inherit;
}
.panel-head button:hover{background:rgba(255,255,255,.06);color:var(--text)}
.panel-head button.primary{background:var(--accent);color:#fff;border-color:var(--accent)}
.panel-head button.primary:hover{background:var(--accent-hover)}
.panel-body{flex:1;overflow:auto;padding:12px 14px}
.panel-body::-webkit-scrollbar{width:6px}
.panel-body::-webkit-scrollbar-thumb{background:#4a4f58;border-radius:4px}
.panel-body h3{font-size:10px;letter-spacing:.1em;text-transform:uppercase;color:var(--muted);margin:14px 0 8px;font-weight:600}
.panel-body h3:first-child{margin-top:4px}
.panel-intro{margin:0 0 10px;font-size:12px;color:var(--muted);line-height:1.4}
label.row{
  display:flex;gap:10px;align-items:center;min-height:30px;font-size:13px;color:var(--text);
  padding:2px 4px;border-radius:5px;cursor:pointer;
}
label.row:hover{background:rgba(255,255,255,.04)}
label.row input[type=checkbox]{accent-color:var(--accent);width:14px;height:14px;cursor:pointer}

#propsTable{width:100%;border-collapse:collapse;font-size:12.5px}
#propsTable th,#propsTable td{text-align:left;padding:7px 8px;border-bottom:1px solid var(--border);vertical-align:top}
#propsTable th{width:38%;color:var(--muted);font-weight:500;font-size:11px;text-transform:uppercase;letter-spacing:.03em}
#propsTable td{font-family:var(--mono);font-size:12px;color:var(--text);word-break:break-word}
#propsEmpty{
  padding:24px 12px;text-align:center;color:var(--muted);font-size:13px;line-height:1.5;
  background:rgba(255,255,255,.03);border-radius:8px;border:1px dashed var(--border);
}

.cut-row{display:flex;gap:6px;margin:8px 0 12px}
.cut-row button{
  flex:1;min-height:32px;border-radius:6px;border:1px solid var(--border);
  background:rgba(255,255,255,.04);cursor:pointer;font-size:13px;font-weight:600;font-family:inherit;
  color:var(--muted);transition:all .12s;
}
.cut-row button:hover,.cut-row button.active{background:var(--accent);color:#fff;border-color:var(--accent)}
input[type=range]{width:100%;accent-color:var(--accent);margin:6px 0 10px}

#hud{
  position:fixed;left:50%;bottom:78px;transform:translateX(-50%);z-index:35;
  background:rgba(30,32,36,.95);color:var(--text);border:1px solid var(--border);
  border-radius:8px;padding:8px 14px;font-size:12.5px;font-weight:500;
  box-shadow:var(--shadow);display:none;pointer-events:none;
  max-width:min(900px,94vw);text-align:center;line-height:1.45;
}
#err{
  position:fixed;left:50%;top:50%;transform:translate(-50%,-50%);z-index:50;
  background:var(--surface);border:1px solid #5c2b2b;padding:18px 22px;border-radius:var(--radius);
  box-shadow:var(--shadow);display:none;max-width:min(400px,90vw);color:var(--danger);font-size:14px;line-height:1.5;
}

/* View cube corner hint */
#viewHint{
  position:fixed;top:calc(var(--bar-h) + 12px);right:12px;z-index:20;
  font-size:11px;color:var(--muted);background:rgba(30,32,36,.7);
  padding:4px 10px;border-radius:6px;border:1px solid var(--border);pointer-events:none;
}

@media (max-width:700px){
  #bottombar{left:8px;right:8px;transform:none;overflow-x:auto;justify-content:flex-start}
  .panel{top:auto;bottom:0;left:0;right:0;width:100%;max-height:50vh;border-radius:12px 12px 0 0}
  .panel.left,.panel.right{left:0;right:0}
  #viewHint{display:none}
}
</style>
</head>
<body>
<canvas id=""c""></canvas>
<div id=""topbar"">
  <span class=""brand"">{{BRAND}}</span>
  <span class=""sep"">›</span>
  <span class=""doc"" title=""{{PROJECT_NAME}}"">{{PROJECT_NAME}} · {{VIEW_NAME}}</span>
  <span class=""spacer""></span>
</div>
<div id=""viewHint"">Orbit · Scroll zoom · Right-drag pan</div>
<aside id=""panelLayers"" class=""panel left closed"">
  <div class=""panel-head""><h2>Model browser</h2><button type=""button"" id=""closeLayers"">Close</button></div>
  <div class=""panel-body"" id=""layersBody"">
    <p class=""panel-intro"">{{PROJECT_NAME}} · {{VIEW_NAME}}</p>
  </div>
</aside>
<aside id=""panelProps"" class=""panel right closed"">
  <div class=""panel-head"">
    <h2>Properties</h2>
    <button type=""button"" id=""btnHideProps"" class=""primary"" title=""Hide selected"">Hide</button>
    <button type=""button"" id=""expandProps"">Expand</button>
    <button type=""button"" id=""minProps"">Min</button>
    <button type=""button"" id=""closeProps"">Close</button>
  </div>
  <div class=""panel-body"">
    <div id=""propsEmpty"">Select an element in the model<br/>to inspect its properties.</div>
    <table id=""propsTable"" style=""display:none""><tbody></tbody></table>
  </div>
</aside>
<aside id=""panelCut"" class=""panel right closed"" style=""width:min(260px,90vw)"">
  <div class=""panel-head""><h2>Section</h2><button type=""button"" id=""closeCut"">Close</button></div>
  <div class=""panel-body"">
    <p class=""panel-intro"">Slice the model along an axis.</p>
    <div class=""cut-row"">
      <button type=""button"" data-axis=""x"">X</button>
      <button type=""button"" data-axis=""y"" class=""active"">Y</button>
      <button type=""button"" data-axis=""z"">Z</button>
    </div>
    <label class=""row"" style=""margin-bottom:4px"">Position</label>
    <input type=""range"" id=""cutPos"" min=""0"" max=""1000"" value=""500""/>
    <label class=""row""><input type=""checkbox"" id=""cutEnable""/> Enable cut</label>
    <label class=""row""><input type=""checkbox"" id=""cutFlip""/> Flip side</label>
  </div>
</aside>
<div id=""bottombar"">
  <button type=""button"" id=""btnHome"" title=""Home view""><span class=""ico"">⌂</span>Home</button>
  <button type=""button"" id=""btnFit"" title=""Fit to view""><span class=""ico"">▣</span>Fit</button>
  <span class=""vdiv""></span>
  <button type=""button"" id=""btnOrbit"" class=""active"" title=""Orbit""><span class=""ico"">↻</span>Orbit</button>
  <button type=""button"" id=""btnSelect"" title=""Select""><span class=""ico"">⌖</span>Select</button>
  <button type=""button"" id=""btnMeasure"" title=""Measure""><span class=""ico"">⟷</span>Measure</button>
  <span class=""vdiv""></span>
  <button type=""button"" id=""btnFocus"" title=""Focus selected""><span class=""ico"">◎</span>Focus</button>
  <button type=""button"" id=""btnHide"" title=""Hide selected (H)""><span class=""ico"">✕</span>Hide</button>
  <button type=""button"" id=""btnShowAll"" title=""Show all""><span class=""ico"">☰</span>Show</button>
  <button type=""button"" id=""btnClear"" class=""wide"" title=""Clear measure""><span class=""ico"">○</span>Clear</button>
  <span class=""vdiv""></span>
  <button type=""button"" id=""btnLayers"" class=""wide"" title=""Model browser""><span class=""ico"">☰</span>Browser</button>
  <button type=""button"" id=""btnProps"" class=""wide"" title=""Properties""><span class=""ico"">☰</span>Props</button>
  <button type=""button"" id=""btnCut"" class=""wide"" title=""Section cut""><span class=""ico"">✂</span>Section</button>
</div>
<div id=""hud""></div>
<div id=""err""></div>
<script type=""importmap"">{""imports"":{""three"":""https://unpkg.com/three@0.160.0/build/three.module.js"",""three/addons/"":""https://unpkg.com/three@0.160.0/examples/jsm/""}}</script>
<script type=""module"">
import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';

const GLTF = {{GLTF_JSON}};
const canvas = document.getElementById('c');
const hud = document.getElementById('hud');
const errBox = document.getElementById('err');
const propsEmpty = document.getElementById('propsEmpty');
const propsTable = document.getElementById('propsTable');
const propsBody = propsTable.querySelector('tbody');

function showErr(msg) {
  errBox.style.display = 'block';
  errBox.textContent = msg;
  console.error(msg);
}

const renderer = new THREE.WebGLRenderer({ canvas: canvas, antialias: true });
renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.5));
renderer.setClearColor(0x3c4046, 1);
renderer.localClippingEnabled = true;

const scene = new THREE.Scene();
scene.background = new THREE.Color(0x3c4046);

const camera = new THREE.PerspectiveCamera(50, 1, 0.1, 100000);
camera.position.set(40, 30, 40);

const controls = new OrbitControls(camera, canvas);
controls.enableDamping = true;
controls.dampingFactor = 0.08;
controls.screenSpacePanning = true;

scene.add(new THREE.AmbientLight(0xffffff, 0.65));
scene.add(new THREE.HemisphereLight(0xffffff, 0x444444, 0.5));
const dir = new THREE.DirectionalLight(0xffffff, 0.75);
dir.position.set(40, 60, 20);
scene.add(dir);
const fill = new THREE.DirectionalLight(0xffffff, 0.3);
fill.position.set(-30, 20, -20);
scene.add(fill);

const raycaster = new THREE.Raycaster();
const pointer = new THREE.Vector2();
let root = null;
let mode = 'orbit';
let measurePts = [];
const measureGroup = new THREE.Group();
scene.add(measureGroup);
let selected = null;
let selectedBackup = null;
let hiddenMeshes = [];
let cutAxis = 'y';
const clipPlane = new THREE.Plane();
const modelBox = new THREE.Box3();
const modelSize = new THREE.Vector3(1, 1, 1);
let homeCam = null;

const panelLayers = document.getElementById('panelLayers');
const panelProps = document.getElementById('panelProps');
const panelCut = document.getElementById('panelCut');

function openP(p) { p.classList.remove('closed'); }
function closeP(p) { p.classList.add('closed'); }
function toggleP(p) { p.classList.toggle('closed'); }

document.getElementById('btnLayers').onclick = function () { toggleP(panelLayers); };
document.getElementById('closeLayers').onclick = function () { closeP(panelLayers); };
document.getElementById('btnProps').onclick = function () { openP(panelProps); };
document.getElementById('closeProps').onclick = function () { closeP(panelProps); };
document.getElementById('expandProps').onclick = function () { panelProps.classList.add('expanded'); openP(panelProps); };
document.getElementById('minProps').onclick = function () { panelProps.classList.remove('expanded'); };
document.getElementById('btnCut').onclick = function () { toggleP(panelCut); };
document.getElementById('closeCut').onclick = function () { closeP(panelCut); };

function setMode(m) {
  mode = m;
  document.getElementById('btnOrbit').classList.toggle('active', m === 'orbit');
  document.getElementById('btnSelect').classList.toggle('active', m === 'select');
  document.getElementById('btnMeasure').classList.toggle('active', m === 'measure');
  controls.enabled = m !== 'measure';
  canvas.style.cursor = (m === 'select' || m === 'measure') ? 'crosshair' : 'default';
}
document.getElementById('btnOrbit').onclick = function () { setMode('orbit'); };
document.getElementById('btnSelect').onclick = function () { setMode('select'); };
document.getElementById('btnMeasure').onclick = function () { setMode('measure'); };
document.getElementById('btnFocus').onclick = function () { if (selected) focusObj(selected); };

function hideSelected() {
  if (!selected || !root) return;
  var info = gatherUD(selected);
  var eid = info.elementId;
  var count = 0;
  root.traverse(function (o) {
    if (!o.isMesh || !o.visible) return;
    var ud = gatherUD(o);
    var match = false;
    if (eid != null && ud.elementId != null && ud.elementId === eid) match = true;
    if (!match && o === selected) match = true;
    if (!match) return;
    o.visible = false;
    if (hiddenMeshes.indexOf(o) < 0) hiddenMeshes.push(o);
    count++;
  });
  clearSelection();
  hud.style.display = 'block';
  hud.textContent = 'Hidden ' + count + ' mesh(es). Use Show to restore.';
  setTimeout(function () { if (hud.textContent.indexOf('Hidden') === 0) hud.style.display = 'none'; }, 2500);
}

function showAllHidden() {
  var n = hiddenMeshes.length;
  for (var i = 0; i < hiddenMeshes.length; i++) {
    if (hiddenMeshes[i]) hiddenMeshes[i].visible = true;
  }
  hiddenMeshes = [];
  hud.style.display = 'block';
  hud.textContent = n ? ('Restored ' + n + ' hidden mesh(es).') : 'Nothing was hidden.';
  setTimeout(function () { if (hud.textContent.indexOf('Restored') === 0 || hud.textContent.indexOf('Nothing') === 0) hud.style.display = 'none'; }, 2000);
}

document.getElementById('btnHide').onclick = hideSelected;
document.getElementById('btnShowAll').onclick = showAllHidden;
var hidePropsBtn = document.getElementById('btnHideProps');
if (hidePropsBtn) hidePropsBtn.onclick = hideSelected;

document.getElementById('btnClear').onclick = function () {
  measurePts = [];
  while (measureGroup.children.length) {
    var o = measureGroup.children.pop();
    if (o.geometry) o.geometry.dispose();
    if (o.material) o.material.dispose();
  }
  hud.style.display = 'none';
};

document.getElementById('btnHome').onclick = function () {
  if (!homeCam) return;
  camera.position.copy(homeCam.pos);
  controls.target.copy(homeCam.target);
  camera.near = homeCam.near;
  camera.far = homeCam.far;
  camera.updateProjectionMatrix();
  controls.update();
};
document.getElementById('btnFit').onclick = function () {
  if (!modelBox.isEmpty()) frameCamera(modelBox);
};

document.querySelectorAll('#panelCut [data-axis]').forEach(function (btn) {
  btn.onclick = function () {
    cutAxis = btn.getAttribute('data-axis');
    document.querySelectorAll('#panelCut [data-axis]').forEach(function (b) {
      b.classList.toggle('active', b.getAttribute('data-axis') === cutAxis);
    });
    updateClip();
  };
});
document.getElementById('cutPos').oninput = updateClip;
document.getElementById('cutEnable').onchange = updateClip;
document.getElementById('cutFlip').onchange = updateClip;

function updateClip() {
  var enabled = document.getElementById('cutEnable').checked;
  if (!enabled) {
    renderer.clippingPlanes = [];
    applyClip([]);
    return;
  }
  var flip = document.getElementById('cutFlip').checked ? -1 : 1;
  var t = Number(document.getElementById('cutPos').value) / 1000;
  var min = modelBox.min, max = modelBox.max;
  var point = new THREE.Vector3(
    min.x + (max.x - min.x) * (cutAxis === 'x' ? t : 0.5),
    min.y + (max.y - min.y) * (cutAxis === 'y' ? t : 0.5),
    min.z + (max.z - min.z) * (cutAxis === 'z' ? t : 0.5)
  );
  var normal = new THREE.Vector3(cutAxis === 'x' ? 1 : 0, cutAxis === 'y' ? 1 : 0, cutAxis === 'z' ? 1 : 0);
  normal.multiplyScalar(flip);
  clipPlane.setFromNormalAndCoplanarPoint(normal, point);
  renderer.clippingPlanes = [clipPlane];
  applyClip([clipPlane]);
}
function applyClip(planes) {
  if (!root) return;
  root.traverse(function (o) {
    if (!o.isMesh || !o.material) return;
    var mats = Array.isArray(o.material) ? o.material : [o.material];
    mats.forEach(function (m) { m.clippingPlanes = planes; m.needsUpdate = true; });
  });
}

function clearSelection() {
  if (selected && selectedBackup) {
    var mats = Array.isArray(selected.material) ? selected.material : [selected.material];
    mats.forEach(function (m, i) {
      if (!m || !selectedBackup[i]) return;
      m.emissive.copy(selectedBackup[i].emissive);
      m.emissiveIntensity = selectedBackup[i].emissiveIntensity;
      m.needsUpdate = true;
    });
  }
  selected = null;
  selectedBackup = null;
  propsEmpty.style.display = 'block';
  propsEmpty.innerHTML = 'Select an element in the model<br/>to inspect its properties.';
  propsTable.style.display = 'none';
  propsBody.innerHTML = '';
}

function gatherUD(mesh) {
  var out = { level: '', category: '', name: '', elementId: null, uniqueId: '', properties: {} };
  var p = mesh;
  while (p) {
    var ud = p.userData || {};
    if (!out.level && ud.level) out.level = ud.level;
    if (!out.category && ud.category) out.category = ud.category;
    if (!out.name && ud.name) out.name = ud.name;
    if (out.elementId == null && ud.elementId != null) out.elementId = ud.elementId;
    if (!out.uniqueId && ud.uniqueId) out.uniqueId = ud.uniqueId;
    if (ud.properties && typeof ud.properties === 'object') {
      for (var k in ud.properties) out.properties[k] = ud.properties[k];
    }
    p = p.parent;
  }
  return out;
}

function showProps(info) {
  propsBody.innerHTML = '';
  var rows = [];
  if (info.name) rows.push(['Name', info.name]);
  if (info.category) rows.push(['Category', info.category]);
  if (info.level) rows.push(['Level', info.level]);
  if (info.elementId != null) rows.push(['Element Id', String(info.elementId)]);
  if (info.uniqueId) rows.push(['Unique Id', info.uniqueId]);
  var keys = Object.keys(info.properties || {}).sort();
  for (var i = 0; i < keys.length; i++) rows.push([keys[i], info.properties[keys[i]]]);
  if (!rows.length) {
    propsEmpty.style.display = 'block';
    propsTable.style.display = 'none';
    return;
  }
  propsEmpty.style.display = 'none';
  propsTable.style.display = 'table';
  for (var j = 0; j < rows.length; j++) {
    var tr = document.createElement('tr');
    var th = document.createElement('th'); th.textContent = rows[j][0];
    var td = document.createElement('td'); td.textContent = rows[j][1];
    tr.appendChild(th); tr.appendChild(td);
    propsBody.appendChild(tr);
  }
  openP(panelProps);
}

function selectObj(obj) {
  clearSelection();
  var mesh = obj;
  while (mesh && !mesh.isMesh) mesh = mesh.parent;
  if (!mesh || !mesh.isMesh) return;
  selected = mesh;
  var mats = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
  selectedBackup = mats.map(function (m) {
    return {
      emissive: m.emissive ? m.emissive.clone() : new THREE.Color(0, 0, 0),
      emissiveIntensity: m.emissiveIntensity != null ? m.emissiveIntensity : 0
    };
  });
  mats.forEach(function (m) {
    if (!m.emissive) m.emissive = new THREE.Color(0x0696d7);
    else m.emissive.set(0x0696d7);
    m.emissiveIntensity = 0.55;
    m.needsUpdate = true;
  });
  var box = new THREE.Box3().setFromObject(mesh);
  controls.target.copy(box.getCenter(new THREE.Vector3()));
  showProps(gatherUD(mesh));
}

function focusObj(obj) {
  var box = new THREE.Box3().setFromObject(obj);
  if (box.isEmpty()) return;
  var center = box.getCenter(new THREE.Vector3());
  var size = box.getSize(new THREE.Vector3());
  var span = Math.max(size.x, size.y, size.z, 0.5);
  controls.target.copy(center);
  var dirv = camera.position.clone().sub(controls.target);
  if (dirv.lengthSq() < 1e-8) dirv.set(1, 0.8, 1);
  dirv.normalize();
  camera.position.copy(center).add(dirv.multiplyScalar(span * 2.5));
  camera.near = Math.max(span / 200, 0.01);
  camera.far = Math.max(span * 200, 1000);
  camera.updateProjectionMatrix();
  controls.update();
}

function onPointer(ev) {
  if (!root || (mode !== 'select' && mode !== 'measure')) return;
  var rect = canvas.getBoundingClientRect();
  pointer.x = ((ev.clientX - rect.left) / rect.width) * 2 - 1;
  pointer.y = -((ev.clientY - rect.top) / rect.height) * 2 + 1;
  raycaster.setFromCamera(pointer, camera);
  var hits = raycaster.intersectObject(root, true);
  if (!hits.length) {
    if (mode === 'select') clearSelection();
    return;
  }
  if (mode === 'select') { selectObj(hits[0].object); return; }
  var p = hits[0].point.clone();
  if (measurePts.length >= 2) {
    measurePts = [];
    while (measureGroup.children.length) {
      var o = measureGroup.children.pop();
      if (o.geometry) o.geometry.dispose();
      if (o.material) o.material.dispose();
    }
  }
  measurePts.push(p);
  var r = Math.max(modelSize.length() * 0.003, 0.05);
  var marker = new THREE.Mesh(new THREE.SphereGeometry(r, 12, 12), new THREE.MeshBasicMaterial({ color: 0x0696d7 }));
  marker.position.copy(p);
  measureGroup.add(marker);
  if (measurePts.length === 2) {
    var geo = new THREE.BufferGeometry().setFromPoints(measurePts);
    measureGroup.add(new THREE.Line(geo, new THREE.LineBasicMaterial({ color: 0x0696d7 })));
    var a = measurePts[0], b = measurePts[1];
    var dx = Math.abs(b.x - a.x);
    var dy = Math.abs(b.y - a.y);
    var dz = Math.abs(b.z - a.z);
    var dist = a.distanceTo(b);
    function fmtFt(v) { return v.toFixed(3) + ' ft'; }
    function fmtM(v) { return (v * 0.3048).toFixed(3) + ' m'; }
    hud.style.display = 'block';
    hud.innerHTML =
      '<strong>Diagonal</strong> ' + fmtM(dist) + ' · ' + fmtFt(dist) +
      ' &nbsp;|&nbsp; <strong>X</strong> ' + fmtM(dx) + ' · ' + fmtFt(dx) +
      ' &nbsp;|&nbsp; <strong>Y</strong> ' + fmtM(dy) + ' · ' + fmtFt(dy) +
      ' &nbsp;|&nbsp; <strong>Z</strong> ' + fmtM(dz) + ' · ' + fmtFt(dz);
  } else {
    hud.style.display = 'block';
    hud.textContent = 'Click second point…';
  }
}
canvas.addEventListener('pointerdown', onPointer);

window.addEventListener('keydown', function (ev) {
  if (ev.target && (ev.target.tagName === 'INPUT' || ev.target.tagName === 'TEXTAREA')) return;
  if ((ev.key === 'h' || ev.key === 'H' || ev.key === 'Delete') && mode === 'select') {
    hideSelected();
    ev.preventDefault();
  }
  if ((ev.key === 'h' || ev.key === 'H') && ev.ctrlKey && ev.shiftKey) {
    showAllHidden();
    ev.preventDefault();
  }
});

function frameCamera(box) {
  var center = box.getCenter(new THREE.Vector3());
  box.getSize(modelSize);
  var span = Math.max(modelSize.x, modelSize.y, modelSize.z, 1);
  controls.target.copy(center);
  camera.position.set(center.x + span * 1.2, center.y + span * 0.9, center.z + span * 1.2);
  camera.near = Math.max(span / 500, 0.01);
  camera.far = span * 100;
  camera.updateProjectionMatrix();
  controls.update();
  homeCam = {
    pos: camera.position.clone(),
    target: controls.target.clone(),
    near: camera.near,
    far: camera.far
  };
}

function onModel(gltf) {
  root = gltf.scene;
  var meshCount = 0;
  root.traverse(function (o) {
    if (!o.isMesh) return;
    meshCount++;
    if (o.material) {
      var mats = Array.isArray(o.material) ? o.material : [o.material];
      mats.forEach(function (m) {
        m.side = THREE.DoubleSide;
        if (m.transparent && m.opacity < 0.05) m.opacity = 1;
        if (!m.emissive) m.emissive = new THREE.Color(0, 0, 0);
        m.needsUpdate = true;
      });
    }
  });
  if (meshCount === 0) {
    showErr('glTF loaded but contains 0 meshes.');
    return;
  }
  scene.add(root);
  modelBox.setFromObject(root);
  if (modelBox.isEmpty()) {
    showErr('Model bounding box is empty.');
    return;
  }
  frameCamera(modelBox);
  renderer.clippingPlanes = [];
  applyClip([]);
  hud.style.display = 'none';

  var byLevel = {}, byCat = {};
  root.traverse(function (o) {
    var ex = o.userData || {};
    if (ex.level) {
      if (!byLevel[ex.level]) byLevel[ex.level] = [];
      byLevel[ex.level].push(o);
    }
    if (ex.category) {
      if (!byCat[ex.category]) byCat[ex.category] = [];
      byCat[ex.category].push(o);
    }
  });
  var boxEl = document.getElementById('layersBody');
  function group(title, map) {
    var h = document.createElement('h3');
    h.textContent = title;
    boxEl.appendChild(h);
    Object.keys(map).sort().forEach(function (k) {
      var lab = document.createElement('label');
      lab.className = 'row';
      var cb = document.createElement('input');
      cb.type = 'checkbox';
      cb.checked = true;
      cb.onchange = function () {
        map[k].forEach(function (o) { o.visible = cb.checked; });
      };
      lab.appendChild(cb);
      lab.appendChild(document.createTextNode(' ' + k));
      boxEl.appendChild(lab);
    });
  }
  group('Levels', byLevel);
  group('Categories', byCat);
  console.log('3D HTML Viewer loaded meshes:', meshCount);
}

hud.style.display = 'block';
hud.textContent = 'Loading model…';

try {
  if (!GLTF || !GLTF.meshes || !GLTF.meshes.length) {
    showErr('Export contained no meshes. Re-export with categories selected.');
  } else {
    var loader = new GLTFLoader();
    var jsonText = JSON.stringify(GLTF);
    loader.parse(jsonText, '', function (gltf) {
      onModel(gltf);
    }, function (err) {
      showErr('Failed to parse model: ' + (err && err.message ? err.message : err));
    });
  }
} catch (e) {
  showErr('Load error: ' + e);
}

function resize() {
  var w = window.innerWidth, h = window.innerHeight;
  renderer.setSize(w, h, false);
  camera.aspect = w / h;
  camera.updateProjectionMatrix();
}
window.addEventListener('resize', resize);
resize();

(function loop() {
  controls.update();
  renderer.render(scene, camera);
  requestAnimationFrame(loop);
})();
</script>
</body>
</html>";
    }
}
