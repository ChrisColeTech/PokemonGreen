import { FastifyInstance } from 'fastify';
import { randomUUID } from 'crypto';
import {
  runExtraction,
  type ExtractionConfig,
  type ExtractionProgress,
  type ExtractedGroupResult,
  type ExtractionPhase,
} from '../lib/extraction.js';

// ---------------------------------------------------------------------------
// In-memory job tracker
// ---------------------------------------------------------------------------

interface ExtractionJob {
  id: string;
  config: ExtractionConfig;
  progress: ExtractionProgress;
  results: ExtractedGroupResult[];
  cancelled: boolean;
  /** Resolves when the extraction finishes (or fails/cancels). */
  promise: Promise<void> | null;
}

const jobs = new Map<string, ExtractionJob>();

// ---------------------------------------------------------------------------
// Archive presets
// ---------------------------------------------------------------------------

interface ArchivePresetInfo {
  id: string;
  label: string;
  subpath: string;
  description: string;
}

const ARCHIVE_PRESETS: ArchivePresetInfo[] = [
  {
    id: 'pokemon-models',
    label: 'Pokemon Models (a/0/9/4)',
    subpath: 'a/0/9/4',
    description: 'All Pokemon 3D models, textures, and skeletal animations',
  },
  {
    id: 'pokemon-textures',
    label: 'Pokemon Textures (a/0/9/5)',
    subpath: 'a/0/9/5',
    description: 'Pokemon texture-only archive (shiny, alternate forms)',
  },
  {
    id: 'trainer-models',
    label: 'Trainer Models (a/0/9/6)',
    subpath: 'a/0/9/6',
    description: 'Trainer battle models and animations',
  },
  {
    id: 'trainer-overworld',
    label: 'Trainer Overworld (a/1/0/5)',
    subpath: 'a/1/0/5',
    description: 'Trainer overworld sprites / mini-models',
  },
];

// ---------------------------------------------------------------------------
// Request / response schemas
// ---------------------------------------------------------------------------

interface StartBody {
  garcPath: string;
  outputDir: string;
  splitModelAnims: boolean;
  entryLimit?: number;
  deriveFolderNames?: boolean;
}

interface StatusParams {
  jobId: string;
}

interface CancelParams {
  jobId: string;
}

interface ResultsParams {
  jobId: string;
}

// ---------------------------------------------------------------------------
// Route plugin
// ---------------------------------------------------------------------------

export default async function extractionRoutes(app: FastifyInstance) {

  // ── POST /api/extraction/start ──────────────────────────────────────
  app.post<{ Body: StartBody }>('/api/extraction/start', async (request, reply) => {
    const { garcPath, outputDir, splitModelAnims, entryLimit, deriveFolderNames } = request.body;

    if (!garcPath || !outputDir) {
      return reply.status(400).send({ error: 'garcPath and outputDir are required' });
    }

    const jobId = randomUUID();

    const initialProgress: ExtractionProgress = {
      phase: 'idle',
      stats: {
        totalEntries: 0,
        processedEntries: 0,
        groupsFound: 0,
        modelsExported: 0,
        texturesExported: 0,
        clipsExported: 0,
        parseErrors: 0,
        exportErrors: 0,
      },
      logLines: [],
      elapsedSeconds: 0,
    };

    const job: ExtractionJob = {
      id: jobId,
      config: {
        garcPath,
        outputDir,
        splitModelAnims,
        entryLimit: entryLimit ?? undefined,
        deriveFolderNames: deriveFolderNames ?? true,
      },
      progress: initialProgress,
      results: [],
      cancelled: false,
      promise: null,
    };

    jobs.set(jobId, job);

    // Start extraction in background
    job.promise = (async () => {
      try {
        const results = await runExtraction(
          job.config,
          (progress) => {
            job.progress = progress;
          },
          () => job.cancelled,
        );

        if (job.cancelled) {
          job.progress = {
            ...job.progress,
            phase: 'stopped',
            logLines: [...job.progress.logLines, '', '--- Extraction stopped by user ---'],
          };
        } else {
          job.results = results;
          // The extraction lib already set phase to 'done' via the callback
        }
      } catch (err: any) {
        job.progress = {
          ...job.progress,
          phase: 'error',
          logLines: [...job.progress.logLines, '', `Fatal error: ${err.message ?? String(err)}`],
        };
      }
    })();

    return { jobId };
  });

  // ── GET /api/extraction/status/:jobId ───────────────────────────────
  app.get<{ Params: StatusParams }>('/api/extraction/status/:jobId', async (request, reply) => {
    const { jobId } = request.params;
    const job = jobs.get(jobId);
    if (!job) {
      return reply.status(404).send({ error: `Job not found: ${jobId}` });
    }

    const { phase, stats, logLines, elapsedSeconds } = job.progress;

    return {
      jobId,
      phase,
      stats,
      logLines,
      elapsedSeconds,
      complete: phase === 'done' || phase === 'error' || phase === 'stopped',
    };
  });

  // ── POST /api/extraction/cancel/:jobId ──────────────────────────────
  app.post<{ Params: CancelParams }>('/api/extraction/cancel/:jobId', async (request, reply) => {
    const { jobId } = request.params;
    const job = jobs.get(jobId);
    if (!job) {
      return reply.status(404).send({ error: `Job not found: ${jobId}` });
    }

    job.cancelled = true;

    return { jobId, cancelled: true };
  });

  // ── GET /api/extraction/results/:jobId ──────────────────────────────
  app.get<{ Params: ResultsParams }>('/api/extraction/results/:jobId', async (request, reply) => {
    const { jobId } = request.params;
    const job = jobs.get(jobId);
    if (!job) {
      return reply.status(404).send({ error: `Job not found: ${jobId}` });
    }

    return {
      jobId,
      phase: job.progress.phase,
      groups: job.results,
    };
  });

  // ── GET /api/extraction/presets ─────────────────────────────────────
  app.get('/api/extraction/presets', async () => {
    return ARCHIVE_PRESETS;
  });
}
