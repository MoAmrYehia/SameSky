// Dev-only lineup for iterating on the sculpted character kit (not part of the production build).
import { ACESFilmicToneMapping, Color, DirectionalLight, Group, HemisphereLight, NeutralToneMapping, PerspectiveCamera, PMREMGenerator, Quaternion, Scene, SRGBColorSpace, Vector3, WebGLRenderer } from 'three';
import { HDRLoader } from 'three/examples/jsm/loaders/HDRLoader.js';
import { CharacterModel, loadCharacterKit } from '../shared/character';
import type { AvatarDefinition } from '../shared/contracts';

const params = new URLSearchParams(location.search);
const view = params.get('view') ?? 'lineup';
const renderer = new WebGLRenderer({ antialias: true });
renderer.setSize(innerWidth, innerHeight);
renderer.setPixelRatio(1);
renderer.outputColorSpace = SRGBColorSpace;
renderer.toneMapping = params.get('tm') === 'aces' ? ACESFilmicToneMapping : NeutralToneMapping;
document.body.appendChild(renderer.domElement);
const scene = new Scene();
scene.background = new Color('#d9cbb6');
const pmrem = new PMREMGenerator(renderer);
const hdr = await new HDRLoader().loadAsync('/assets/env/lounge_1k.hdr');
scene.environment = pmrem.fromEquirectangular(hdr).texture;
scene.environmentIntensity = 0.8;
scene.add(new HemisphereLight(0xfff1e0, 0x5a4636, 0.4));
const key = new DirectionalLight(0xffe0bd, 2.2);
key.position.set(1.5, 2.5, 3);
scene.add(key);
const rim = new DirectionalLight(0xffc48a, 1.4);
rim.position.set(-2, 2, -2);
scene.add(rim);

const kit = await loadCharacterKit();
const people: AvatarDefinition[] = [
  { skin: '#e8b896', hair: 'wavy', hairColor: '#4a2f1f', top: '#e9e0cf', accessory: 'earrings', face: 'soft' },
  { skin: '#d29a72', hair: 'short', hairColor: '#2b1d14', top: '#3f6b52', accessory: 'none', face: 'defined' },
  { skin: '#f6d7c3', hair: 'bun', hairColor: '#7a4a2a', top: '#2f4a6b', accessory: 'glasses', face: 'soft' },
  { skin: '#8a5634', hair: 'curly', hairColor: '#1d1d24', top: '#c98f3a', accessory: 'scarf', face: 'defined' },
  { skin: '#b0754d', hair: 'long', hairColor: '#2b1d14', top: '#8a3b3b', accessory: 'none', face: 'soft' },
  { skin: '#f6d7c3', hair: 'buzz', hairColor: '#8e8e8e', top: '#5b4a7a', accessory: 'cap', face: 'defined' },
];
const models: CharacterModel[] = [];
const group = new Group();
scene.add(group);
const spacing = view === 'faces' ? 0.3 : 0.62;
people.forEach((def, i) => {
  const m = new CharacterModel(kit, def, i + 1);
  m.root.position.set((i - (people.length - 1) / 2) * spacing, 0, 0);
  m.gazeTarget = new Vector3(0, 0.62, 2);
  m.setHand(0, new Vector3(-0.17, 0.2, 0.34));
  m.setHand(1, new Vector3(0.17, 0.2, 0.34));
  m.setHead(new Quaternion());
  group.add(m.root);
  models.push(m);
});
const camera = new PerspectiveCamera(view === 'faces' ? 22 : 34, innerWidth / innerHeight, 0.01, 50);
if (view === 'faces') {
  camera.position.set(0, 0.64, 1.25);
  camera.lookAt(0, 0.64, 0);
} else if (view === 'face') {
  group.children.forEach((c, i) => (c.visible = i === Number(params.get('i') ?? 0)));
  group.children.forEach((c) => c.position.set(0, 0, 0));
  camera.fov = 18;
  camera.position.set(0.12, 0.68, 0.62);
  camera.lookAt(0, 0.64, 0);
} else {
  camera.position.set(0, 0.75, 3.1);
  camera.lookAt(0, 0.35, 0);
}
camera.updateProjectionMatrix();
let t = 0;
renderer.setAnimationLoop(() => {
  t += 1 / 60;
  for (const m of models) m.update(1 / 60, 3.3);
  renderer.render(scene, camera);
  (window as unknown as { __ready: boolean }).__ready = true;
});
