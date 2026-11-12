// Dev-only check of the photo-avatar rig path with the TEST MANNEQUIN (tests/fixtures/test-mannequin.glb).
// Two clones of one cached model sit side by side with different poses: verifies skeleton isolation,
// seated legs, arm IK onto a table, chin rest, head turn and morph blinks. Not part of the production build.
//   /dev/rig.html?view=front|side|three
import { BoxGeometry, Color, DirectionalLight, HemisphereLight, Mesh, MeshStandardMaterial, NeutralToneMapping, PerspectiveCamera, PMREMGenerator, Quaternion, Scene, SRGBColorSpace, Vector3, WebGLRenderer } from 'three';
import { HDRLoader } from 'three/examples/jsm/loaders/HDRLoader.js';
import { acquireAvatarModel, SkinnedAvatar } from '../shared/avatarRig';

const params = new URLSearchParams(location.search);
const view = params.get('view') ?? 'three';
const renderer = new WebGLRenderer({ antialias: true });
renderer.setSize(innerWidth, innerHeight);
renderer.setPixelRatio(1);
renderer.outputColorSpace = SRGBColorSpace;
renderer.toneMapping = NeutralToneMapping;
renderer.shadowMap.enabled = true;
document.body.appendChild(renderer.domElement);
const scene = new Scene();
scene.background = new Color('#d8d3cc');
const pmrem = new PMREMGenerator(renderer);
scene.environment = pmrem.fromEquirectangular(await new HDRLoader().loadAsync('/assets/env/lounge_1k.hdr')).texture;
scene.environmentIntensity = 0.6;
scene.add(new HemisphereLight(0xffffff, 0x777777, 0.5));
const key = new DirectionalLight(0xffffff, 1.8);
key.position.set(1.2, 2.4, 2.6);
key.castShadow = true;
scene.add(key);

// a table top 0.31 m above the seat, its near edge 0.36 m in front of the pelvis (like the room)
const TABLE_TOP = 0.31;
const EDGE = 0.36;
const table = new Mesh(new BoxGeometry(1.8, 0.04, 0.7), new MeshStandardMaterial({ color: '#6b4a33', roughness: 0.6 }));
table.position.set(0, TABLE_TOP - 0.02, EDGE + 0.35);
table.receiveShadow = true;
scene.add(table);

const { gltf } = await acquireAvatarModel('dev|mannequin|1', '/tests/fixtures/test-mannequin.glb');
const a = new SkinnedAvatar(gltf, 1);
const b = new SkinnedAvatar(gltf, 2);
a.root.position.x = -0.42;
b.root.position.x = 0.42;
scene.add(a.root, b.root);
// a: forearms on the table, looking at the camera; b: chin on the hand, head turned
a.setHand(0, new Vector3(-0.14, TABLE_TOP + 0.03, EDGE + 0.06));
a.setHand(1, new Vector3(0.15, TABLE_TOP + 0.03, EDGE + 0.08));
b.setHand(0, b.chinRest(new Vector3()));
b.setPole(0, new Vector3(-0.15, -0.8, 0.75));
b.setHand(1, new Vector3(0.15, TABLE_TOP + 0.03, EDGE + 0.08));
b.setHead(new Quaternion().setFromEuler(new (await import('three')).Euler(0.12, -0.35, 0.12, 'YXZ')), new Vector3(0, 0, 0.05));

const camera = new PerspectiveCamera(30, innerWidth / innerHeight, 0.01, 50);
const at = new Vector3(0, 0.55, 0.1);
if (view === 'front') camera.position.set(0, 0.85, 3.0);
else if (view === 'side') camera.position.set(3.0, 0.8, 0.3);
else camera.position.set(1.7, 1.0, 2.6);
camera.lookAt(at);
a.gazeTarget = a.root.worldToLocal(camera.position.clone());
let t = 0;
renderer.setAnimationLoop(() => {
  t += 1 / 60;
  a.update(1 / 60, t);
  b.update(1 / 60, t);
  renderer.render(scene, camera);
  (window as unknown as { __ready: boolean }).__ready = true;
});
