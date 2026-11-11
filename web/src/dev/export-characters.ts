// Dev-only: exports the sample people's sculpted demo characters as plain GLB for the native Quest app
// (web/scripts/export-characters.mjs drives this page). Same kit, same definitions, seated rest pose, metres.
import { Group, Quaternion, Vector3 } from 'three';
import { GLTFExporter } from 'three/examples/jsm/exporters/GLTFExporter.js';
import { CharacterModel, loadCharacterKit } from '../shared/character';
import { SAMPLE_PEOPLE } from '../shared/sample';

const kit = await loadCharacterKit();

function toBase64(buf: ArrayBuffer): string {
  let s = '';
  const bytes = new Uint8Array(buf);
  for (let i = 0; i < bytes.length; i += 0x8000) s += String.fromCharCode(...bytes.subarray(i, i + 0x8000));
  return btoa(s);
}

async function exportCharacter(id: string): Promise<string> {
  const person = SAMPLE_PEOPLE[id];
  if (!person) throw new Error(`unknown sample person ${id}`);
  const m = new CharacterModel(kit, person.avatar, 1);
  m.gazeTarget = new Vector3(0, 0.62, 2);
  m.setHand(0, new Vector3(-0.17, 0.2, 0.34));
  m.setHand(1, new Vector3(0.17, 0.2, 0.34));
  m.setHead(new Quaternion());
  m.breathing = false;
  m.blinking = false;
  for (let i = 0; i < 60; i++) m.update(1 / 60, i / 60);
  // names the native app animates (gaze, breathing) and anchors labels to
  m.root.name = 'character';
  m.chest.name = 'chest';
  m.neck.name = 'neck';
  m.head.name = 'head';
  m.leftWrist.name = 'leftWrist';
  m.rightWrist.name = 'rightWrist';
  m.labelAnchor.name = 'labelAnchor';
  const wrapper = new Group();
  wrapper.name = `${person.name} (sample demo character)`;
  m.root.scale.setScalar(m.displayScale); // export at table scale, in metres
  wrapper.add(m.root);
  wrapper.updateMatrixWorld(true);
  const glb = (await new GLTFExporter().parseAsync(wrapper, { binary: true, onlyVisible: true })) as ArrayBuffer;
  return toBase64(glb);
}

(window as unknown as Record<string, unknown>).__exportCharacter = exportCharacter;
(window as unknown as Record<string, unknown>).__exportReady = true;
