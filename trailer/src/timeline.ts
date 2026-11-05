export const FPS = 30;
export const WIDTH = 1920;
export const HEIGHT = 1080;

/** Where the camera looks: its zoom, and the point of the frame it centres on, from 0 to 1. */
export type View = {scale: number; x: number; y: number};

/** One saved frame of the app, held for a number of video frames. */
export type Shot = {file: string; frames: number};

/** Which corner the caption sits in, chosen to be clear of what the scene shows. */
export type Corner = 'bottom-left' | 'bottom-right' | 'top-right';

export type Scene = {
  caption: string;
  corner: Corner;
  shots: Shot[];
  from: View;
  to: View;
};

const WHOLE: View = {scale: 1, x: 0.5, y: 0.5};

const shot = (file: string, frames: number): Shot => ({file, frames});

export const SCENES: Scene[] = [
  {
    caption: 'See inside any .NET assembly',
    corner: 'bottom-left',
    shots: [
      shot('goto/00002', 8),
      shot('goto/00004', 8),
      shot('goto/00006', 8),
      shot('goto/00007', 8),
      shot('goto/00009', 34),
      shot('goto/00013', 24),
    ],
    from: WHOLE,
    to: {scale: 1.4, x: 0.68, y: 0.25},
  },
  {
    caption: 'Open any package',
    corner: 'bottom-left',
    shots: [shot('nuget/00001', 15), shot('nuget/00006', 50), shot('nuget/00013', 25)],
    from: WHOLE,
    to: {scale: 1.15, x: 0.5, y: 0.45},
  },
  {
    caption: 'Read it at any level',
    corner: 'top-right',
    shots: [shot('languages/00000', 40), shot('languages/00003', 40), shot('languages/00007', 40)],
    from: {scale: 1.25, x: 0.68, y: 0.35},
    to: {scale: 1.4, x: 0.68, y: 0.35},
  },
  {
    caption: 'Find what uses what',
    corner: 'top-right',
    shots: [shot('analyzer/00000', 30), shot('analyzer/00003', 40), shot('analyzer/00006', 80)],
    from: WHOLE,
    to: {scale: 1.5, x: 0.68, y: 0.78},
  },
  {
    caption: 'Map the types',
    corner: 'bottom-left',
    shots: [shot('diagram/00001', 90)],
    from: {scale: 1.2, x: 0.68, y: 0.35},
    to: {scale: 1.2, x: 0.68, y: 0.7},
  },
  {
    caption: 'See what changed',
    corner: 'bottom-right',
    shots: [shot('compare/00001', 90)],
    from: {scale: 1.1, x: 0.6, y: 0.3},
    to: {scale: 1.7, x: 0.68, y: 0.19},
  },
  {
    caption: 'Keep your place',
    corner: 'top-right',
    shots: [shot('bookmarks/00000', 18), shot('bookmarks/00002', 20), shot('bookmarks/00006', 22)],
    from: {scale: 1.2, x: 0.62, y: 0.6},
    to: {scale: 1.35, x: 0.62, y: 0.6},
  },
  {
    caption: 'Export it as a C# project',
    corner: 'bottom-left',
    shots: [
      shot('export/00002', 30),
      shot('export/00003', 14),
      shot('export/00004', 14),
      shot('export/00005', 14),
      shot('export/00006', 14),
      shot('export/00007', 14),
      shot('export/00009', 20),
    ],
    from: WHOLE,
    to: {scale: 1.4, x: 0.68, y: 0.78},
  },
  {
    caption: 'Make it yours',
    corner: 'bottom-right',
    shots: [
      shot('themes/00000', 15),
      shot('themes/00002', 15),
      shot('themes/00004', 15),
      shot('themes/00007', 15),
    ],
    from: {scale: 1.1, x: 0.6, y: 0.45},
    to: {scale: 1.1, x: 0.6, y: 0.45},
  },
];

export const END_CARD_FRAMES = 90;

export const sceneFrames = (scene: Scene): number =>
  scene.shots.reduce((total, s) => total + s.frames, 0);

export const TOTAL_FRAMES =
  SCENES.reduce((total, scene) => total + sceneFrames(scene), 0) + END_CARD_FRAMES;
