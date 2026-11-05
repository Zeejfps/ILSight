export const FPS = 30;
export const WIDTH = 1920;
export const HEIGHT = 1080;

/** Where the camera looks: its zoom, and the point of the frame it centres on, from 0 to 1. */
export type View = {scale: number; x: number; y: number};

/** A point in a saved frame, in its pixels. */
export type Point = {x: number; y: number};

/** One saved frame of the app, held for a number of video frames. By its end the cursor has moved
 * to `click` and pressed it, or to `point` and stayed. */
export type Shot = {file: string; frames: number; click?: Point; point?: Point};

/** Which corner the caption sits in, chosen to be clear of what the scene shows. */
export type Corner = 'bottom-left' | 'bottom-right' | 'top-right';

export type Scene = {
  caption: string;
  corner: Corner;
  shots: Shot[];
  from: View;
  to: View;
  /** The frame the camera starts moving at, so that it leaves what the cursor needs in view. */
  cameraFrom?: number;
};

const WHOLE: View = {scale: 1, x: 0.5, y: 0.5};

const shot = (file: string, frames: number): Shot => ({file, frames});
const click = (file: string, frames: number, x: number, y: number): Shot => ({
  file,
  frames,
  click: {x, y},
});

const FILE_MENU = {x: 31, y: 19};

export const SCENES: Scene[] = [
  {
    caption: 'See inside any .NET assembly',
    corner: 'bottom-left',
    shots: [
      click('goto/00000', 40, 338, 64),
      shot('goto/00001', 12),
      shot('goto/00002', 8),
      shot('goto/00004', 8),
      shot('goto/00006', 8),
      shot('goto/00007', 8),
      click('goto/00009', 45, 765, 206),
      shot('goto/00013', 35),
    ],
    from: WHOLE,
    to: {scale: 1.25, x: 0.68, y: 0.3},
    cameraFrom: 40,
  },
  {
    caption: 'Open any package',
    corner: 'bottom-left',
    shots: [
      click('nuget/00000', 30, FILE_MENU.x, FILE_MENU.y),
      shot('nuget/00001', 14),
      click('nuget/00006', 60, 1601, 984),
      shot('nuget/00013', 40),
    ],
    from: WHOLE,
    to: {scale: 1.2, x: 0.6, y: 0.4},
    cameraFrom: 104,
  },
  {
    caption: 'Read it at any level',
    corner: 'top-right',
    shots: [
      click('languages/00000', 35, 609, 64),
      click('languages/00003', 45, 676, 64),
      shot('languages/00007', 50),
    ],
    from: {scale: 1.2, x: 0.45, y: 0.4},
    to: {scale: 1.3, x: 0.45, y: 0.4},
  },
  {
    caption: 'Find what uses what',
    corner: 'top-right',
    shots: [
      click('analyzer/00001', 35, 250, 1030),
      click('analyzer/00003', 50, 760, 779),
      shot('analyzer/00004', 12),
      shot('analyzer/00006', 75),
    ],
    from: WHOLE,
    to: {scale: 1.5, x: 0.68, y: 0.78},
    cameraFrom: 35,
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
    shots: [{file: 'compare/00001', frames: 90, point: {x: 1215, y: 202}}],
    from: {scale: 1.1, x: 0.6, y: 0.3},
    to: {scale: 1.7, x: 0.68, y: 0.19},
  },
  {
    caption: 'Keep your place',
    corner: 'top-right',
    shots: [
      click('bookmarks/00000', 25, 677, 423),
      click('bookmarks/00002', 35, 800, 913),
      shot('bookmarks/00006', 40),
    ],
    from: {scale: 1.2, x: 0.62, y: 0.6},
    to: {scale: 1.25, x: 0.62, y: 0.6},
  },
  {
    caption: 'Export it as a C# project',
    corner: 'bottom-left',
    shots: [
      click('export/00000', 30, FILE_MENU.x, FILE_MENU.y),
      click('export/00002', 40, 1321, 984),
      shot('export/00003', 14),
      shot('export/00004', 14),
      shot('export/00005', 14),
      shot('export/00006', 14),
      shot('export/00007', 14),
      shot('export/00009', 30),
    ],
    from: WHOLE,
    to: {scale: 1.4, x: 0.68, y: 0.78},
    cameraFrom: 70,
  },
  {
    caption: 'Make it yours',
    corner: 'bottom-right',
    shots: [
      shot('themes/00000', 20),
      shot('themes/00002', 20),
      shot('themes/00004', 20),
      shot('themes/00007', 20),
    ],
    from: {scale: 1.1, x: 0.6, y: 0.45},
    to: {scale: 1.1, x: 0.6, y: 0.45},
  },
];

/** The way of the cursor through a scene: where it is at each of `frames`, and when it presses. */
export type CursorPath = {frames: number[]; xs: number[]; ys: number[]; presses: number[]};

const CURSOR_START: Point = {x: 1120, y: 620};
const MOVE_FRAMES = 16;
const FRAMES_AFTER_PRESS = 10;

export const cursorPathOf = (scene: Scene): CursorPath | null => {
  const path: CursorPath = {frames: [0], xs: [CURSOR_START.x], ys: [CURSOR_START.y], presses: []};
  let at = CURSOR_START;
  let last = 0;
  let start = 0;
  for (const {frames, click: press, point} of scene.shots) {
    const target = press ?? point;
    if (target !== undefined) {
      const arrive = start + frames - FRAMES_AFTER_PRESS;
      const leave = Math.max(last + 1, arrive - MOVE_FRAMES);
      path.frames.push(leave, arrive);
      path.xs.push(at.x, target.x);
      path.ys.push(at.y, target.y);
      if (press !== undefined) path.presses.push(arrive);
      at = target;
      last = arrive;
    }
    start += frames;
  }
  return path.frames.length > 1 ? path : null;
};

export const END_CARD_FRAMES = 90;

export const sceneFrames = (scene: Scene): number =>
  scene.shots.reduce((total, s) => total + s.frames, 0);

export const TOTAL_FRAMES =
  SCENES.reduce((total, scene) => total + sceneFrames(scene), 0) + END_CARD_FRAMES;
