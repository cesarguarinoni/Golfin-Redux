// prepare-textures.mjs
// Downloads, resizes, and writes experimental terrain textures.
// See TEXTURE_EXPERIMENT.md for full spec. Reads manifest.json next to this file.

import { readFile, writeFile, mkdir, access, rm } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { join, dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { Readable } from 'node:stream';
import { pipeline } from 'node:stream/promises';
import { createWriteStream } from 'node:fs';
import { spawn } from 'node:child_process';

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(__dirname, '..', '..');
const SOURCE_DIR = join(__dirname, '_Source');
const OUTPUT_DIR = join(REPO_ROOT, 'Assets', 'Courses', 'Textures_Experimental');

let sharp;
try {
    sharp = (await import('sharp')).default;
} catch {
    console.error('[!] sharp not installed. Run: npm install');
    process.exit(1);
}

// ────────────────────────────────────────────────────────────────────────────
// Helpers

async function ensureDir(p) {
    await mkdir(p, { recursive: true });
}

async function fileExists(p) {
    try {
        await access(p);
        return true;
    } catch {
        return false;
    }
}

function log(stage, msg) {
    const tag = `[${stage}]`.padEnd(10);
    console.log(`${tag} ${msg}`);
}

async function downloadToFile(url, destPath) {
    log('fetch', url);
    const res = await fetch(url, {
        redirect: 'follow',
        headers: { 'User-Agent': 'GOLFIN-TextureExperiment/1.0' },
    });
    if (!res.ok) throw new Error(`HTTP ${res.status} for ${url}`);
    if (!res.body) throw new Error(`No response body for ${url}`);
    await pipeline(Readable.fromWeb(res.body), createWriteStream(destPath));
}

// PowerShell unzip — works on Windows without extra deps
async function unzipPS(zipPath, destDir) {
    await ensureDir(destDir);
    return new Promise((resolveP, rejectP) => {
        const ps = spawn(
            'powershell.exe',
            [
                '-NoProfile',
                '-ExecutionPolicy', 'Bypass',
                '-Command',
                `Expand-Archive -Path "${zipPath}" -DestinationPath "${destDir}" -Force`,
            ],
            { stdio: ['ignore', 'pipe', 'pipe'] }
        );
        let stderr = '';
        ps.stderr.on('data', (d) => { stderr += d.toString(); });
        ps.on('close', (code) => {
            if (code === 0) resolveP();
            else rejectP(new Error(`Expand-Archive exit ${code}: ${stderr}`));
        });
    });
}

// ────────────────────────────────────────────────────────────────────────────
// Source resolution: returns { albedoPath, normalPath } on disk

async function resolveSource(id, src) {
    const srcDir = join(SOURCE_DIR, id);
    await ensureDir(srcDir);

    if (src.type === 'ambientcg-zip') {
        const zipPath = join(srcDir, `${id}.zip`);
        if (!(await fileExists(zipPath))) {
            await downloadToFile(src.url, zipPath);
        } else {
            log('cache', `zip already present: ${id}`);
        }
        // Extract if not yet extracted
        const albedoPath = join(srcDir, src.albedoFile);
        const normalPath = join(srcDir, src.normalFile);
        if (!(await fileExists(albedoPath)) || !(await fileExists(normalPath))) {
            log('unzip', id);
            await unzipPS(zipPath, srcDir);
        }
        if (!(await fileExists(albedoPath))) {
            throw new Error(`Expected albedo not found after unzip: ${albedoPath}`);
        }
        if (!(await fileExists(normalPath))) {
            throw new Error(`Expected normal not found after unzip: ${normalPath}`);
        }
        return { albedoPath, normalPath };
    }

    if (src.type === 'polyhaven-direct') {
        const albedoPath = join(srcDir, 'albedo.jpg');
        const normalPath = join(srcDir, 'normal.jpg');
        if (!(await fileExists(albedoPath))) {
            await downloadToFile(src.albedoUrl, albedoPath);
        } else {
            log('cache', `albedo cached: ${id}`);
        }
        if (!(await fileExists(normalPath))) {
            await downloadToFile(src.normalUrl, normalPath);
        } else {
            log('cache', `normal cached: ${id}`);
        }
        return { albedoPath, normalPath };
    }

    throw new Error(`Unknown source type: ${src.type}`);
}

// ────────────────────────────────────────────────────────────────────────────
// Image processing

async function processImage(inputPath, outputPath, size, brightness, isNormal) {
    let pipe = sharp(inputPath, { failOn: 'truncated' })
        .resize(size, size, { kernel: sharp.kernel.lanczos3, fit: 'cover' });

    // Brightness only for albedo, never for normals
    if (!isNormal && brightness && brightness !== 1.0) {
        pipe = pipe.linear(brightness, 0);
    }

    const quality = isNormal ? 95 : 92;
    await pipe.jpeg({ quality, mozjpeg: false }).toFile(outputPath);
}

// ────────────────────────────────────────────────────────────────────────────
// README generation

function buildReadme(manifest) {
    const lines = [];
    lines.push('# Textures_Experimental');
    lines.push('');
    lines.push('Experimental higher-fidelity terrain textures. **DO NOT** treat as production until visual approval.');
    lines.push('');
    lines.push('Generated by `Tools/TextureExperiment/prepare-textures.mjs`. Re-run that script to rebuild.');
    lines.push('');
    lines.push('## Source attribution');
    lines.push('');
    lines.push('All sources are CC0 (public domain). Attribution not required, but documented for hygiene.');
    lines.push('');
    lines.push('| Output texture | Source |');
    lines.push('|---|---|');
    for (const out of manifest.outputs) {
        const src = manifest.sources[out.source];
        lines.push(`| \`${out.albedo}\` | ${src.attribution} |`);
    }
    lines.push('');
    lines.push('## Swap-in (manual, Cesar)');
    lines.push('');
    lines.push('1. Pick one TerrainLayer, e.g. `Assets/Golf/Courses/lomond-country-club/Data/hole-01-flat/TerrainLayer_T_Fairway_Light.asset`.');
    lines.push('2. Open in Inspector. Set its Diffuse texture to the matching file in this folder.');
    lines.push('3. Test on hole 01 only. If the look is good, batch-apply via a separate spec.');
    lines.push('');
    lines.push('## Swap-back');
    lines.push('');
    lines.push('Delete this folder. Original `Textures_2025(JPG)/` is untouched and TerrainLayer GUIDs still point there.');
    lines.push('');
    lines.push('## Notes');
    lines.push('');
    lines.push('- Albedo files are JPG quality 92, normals JPG quality 95.');
    lines.push('- Mask maps are NOT regenerated. The existing 4×4 A=0 mask maps continue to apply (TerrainLit smoothness comes from mask map alpha = 0, so JPG albedos are safe — see `Docs/AI_CONTEXT.md` "JPG alpha=1.0" note).');
    lines.push('- Light/Dark/BunkerDark/TeeDark variants are brightness-adjusted in this script (see `manifest.json`).');
    lines.push('- Resolutions: 1024 for camera-close surfaces, 512 for OOB and asphalt.');
    lines.push('');
    return lines.join('\n');
}

// ────────────────────────────────────────────────────────────────────────────
// Main

async function main() {
    const manifestPath = join(__dirname, 'manifest.json');
    const manifest = JSON.parse(await readFile(manifestPath, 'utf8'));

    await ensureDir(SOURCE_DIR);
    await ensureDir(OUTPUT_DIR);

    // Write .gitignore in _Source so raw downloads aren't committed
    await writeFile(join(SOURCE_DIR, '.gitignore'), '*\n!.gitignore\n', 'utf8');

    // Resolve all unique sources first
    const resolvedSources = {};
    for (const [id, src] of Object.entries(manifest.sources)) {
        try {
            log('source', `resolving ${id}`);
            resolvedSources[id] = await resolveSource(id, src);
        } catch (e) {
            console.error(`[!] Failed to resolve source "${id}": ${e.message}`);
            throw e;
        }
    }

    // Process each output
    let count = 0;
    for (const out of manifest.outputs) {
        const src = resolvedSources[out.source];
        if (!src) throw new Error(`Output references unknown source: ${out.source}`);

        const albedoOut = join(OUTPUT_DIR, out.albedo);
        log('write', `${out.albedo}  (${out.size}x${out.size}${out.brightness ? `, brightness ${out.brightness}` : ''})`);
        await processImage(src.albedoPath, albedoOut, out.size, out.brightness, false);
        count++;

        if (out.normal) {
            const normalOut = join(OUTPUT_DIR, out.normal);
            log('write', `${out.normal}  (${out.size}x${out.size})`);
            await processImage(src.normalPath, normalOut, out.size, null, true);
            count++;
        }
    }

    // README
    const readmePath = join(OUTPUT_DIR, 'README.md');
    await writeFile(readmePath, buildReadme(manifest), 'utf8');
    log('write', 'README.md');

    console.log('');
    console.log(`✔ Done. Wrote ${count} texture files to:`);
    console.log(`  ${OUTPUT_DIR}`);
    console.log('');
    console.log('Next: see README.md in that folder for swap-in instructions.');
}

main().catch((e) => {
    console.error('');
    console.error('✘ Failed:', e.message);
    process.exit(1);
});
