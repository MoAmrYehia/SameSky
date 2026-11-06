// Prints the bounding boxes of the optimized prop models (metres, model space), used to place seats and anchors.
import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
import { getBounds } from '@gltf-transform/core';
import { MeshoptDecoder } from 'meshoptimizer';

await MeshoptDecoder.ready;
const io = new NodeIO().registerExtensions(ALL_EXTENSIONS).registerDependencies({ 'meshopt.decoder': MeshoptDecoder });
for (const name of process.argv.slice(2)) {
  const doc = await io.read(`public/assets/models/props/${name}.glb`);
  const scene = doc.getRoot().getDefaultScene() ?? doc.getRoot().listScenes()[0];
  const { min, max } = getBounds(scene);
  const size = max.map((v, i) => +(v - min[i]).toFixed(3));
  console.log(name, 'min', min.map((v) => +v.toFixed(3)), 'max', max.map((v) => +v.toFixed(3)), 'size', size);
}
