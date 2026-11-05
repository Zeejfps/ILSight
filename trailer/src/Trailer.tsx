import {loadFont} from '@remotion/google-fonts/Inter';
import type {CSSProperties} from 'react';
import {
  AbsoluteFill,
  Audio,
  Easing,
  Img,
  Sequence,
  interpolate,
  staticFile,
  useCurrentFrame,
} from 'remotion';
import {
  END_CARD_FRAMES,
  HEIGHT,
  type Corner,
  type CursorPath,
  SCENES,
  type Scene,
  TOTAL_FRAMES,
  type View,
  WIDTH,
  cursorPathOf,
  sceneFrames,
} from './timeline';

const {fontFamily} = loadFont('normal', {weights: ['500', '800'], subsets: ['latin']});

const BACKGROUND = '#1b1f26';
const FADE_FRAMES = 5;
const CAPTION_IN_FRAMES = 8;
const PRESS_FRAMES = 5;
const RIPPLE_FRAMES = 16;
const RIPPLE_SIZE = 110;
/** Where the first beat of the track falls, so that the first frame lands on it. */
const MUSIC_FIRST_BEAT = 14;
const CLICK_VOLUME = 0.75;
const CLICK_FRAMES = 15;
const MUSIC_VOLUME = 0.8;
const MUSIC_FADE_FRAMES = 12;
const ACCENT = '#5b9dff';

/** The transform that centres the view's point, kept from showing past the frame's edges. */
const transformOf = ({scale, x, y}: View): string => {
  const roomX = (WIDTH * (scale - 1)) / 2;
  const roomY = (HEIGHT * (scale - 1)) / 2;
  const offsetX = Math.max(-roomX, Math.min(roomX, (0.5 - x) * WIDTH * scale));
  const offsetY = Math.max(-roomY, Math.min(roomY, (0.5 - y) * HEIGHT * scale));
  return `translate(${offsetX}px, ${offsetY}px) scale(${scale})`;
};

const between = (from: number, to: number, progress: number): number =>
  from + (to - from) * progress;

/** A pointer that keeps its size on screen however far the camera has zoomed in, and pulses
 * where it presses. */
const Cursor = ({path, zoom}: {path: CursorPath; zoom: number}) => {
  const frame = useCurrentFrame();
  const along = (values: number[]) =>
    interpolate(frame, path.frames, values, {
      easing: Easing.inOut(Easing.cubic),
      extrapolateLeft: 'clamp',
      extrapolateRight: 'clamp',
    });
  const press = path.presses.find((at) => frame >= at && frame < at + RIPPLE_FRAMES);
  const pressed = press !== undefined && frame < press + PRESS_FRAMES;
  const ripple = press === undefined ? 0 : (frame - press) / RIPPLE_FRAMES;
  return (
    <div
      style={{
        position: 'absolute',
        left: along(path.xs),
        top: along(path.ys),
        transform: `scale(${1 / zoom})`,
      }}
    >
      {press === undefined ? null : (
        <div
          style={{
            position: 'absolute',
            left: -RIPPLE_SIZE / 2,
            top: -RIPPLE_SIZE / 2,
            width: RIPPLE_SIZE,
            height: RIPPLE_SIZE,
            borderRadius: '50%',
            border: `5px solid ${ACCENT}`,
            background: 'rgba(91, 157, 255, 0.25)',
            opacity: 1 - ripple,
            transform: `scale(${0.2 + ripple * 0.8})`,
          }}
        />
      )}
      <svg
        width={44}
        height={44}
        viewBox="0 0 24 24"
        style={{
          position: 'absolute',
          left: -6,
          top: -3,
          transform: `scale(${pressed ? 0.82 : 1})`,
          transformOrigin: '6px 3px',
          filter: 'drop-shadow(0 3px 5px rgba(0, 0, 0, 0.6))',
        }}
      >
        <path
          d="M3.5 1.5 L3.5 19 L8.2 14.7 L11.3 21.8 L14.3 20.5 L11.2 13.5 L17.6 13.5 Z"
          fill="white"
          stroke="black"
          strokeWidth={1.2}
          strokeLinejoin="round"
        />
      </svg>
    </div>
  );
};

const Shots = ({scene}: {scene: Scene}) => {
  const frame = useCurrentFrame();
  const duration = sceneFrames(scene);
  const progress = interpolate(frame, [scene.cameraFrom ?? 0, duration - 1], [0, 1], {
    easing: Easing.bezier(0.45, 0, 0.2, 1),
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });
  const view: View = {
    scale: between(scene.from.scale, scene.to.scale, progress),
    x: between(scene.from.x, scene.to.x, progress),
    y: between(scene.from.y, scene.to.y, progress),
  };
  const path = cursorPathOf(scene);
  let start = 0;
  return (
    <AbsoluteFill style={{overflow: 'hidden', transform: transformOf(view)}}>
      {scene.shots.map((shot) => {
        const from = start;
        start += shot.frames;
        return (
          <Sequence key={shot.file} from={from} durationInFrames={shot.frames} layout="none">
            <Img
              src={staticFile(`frames/${shot.file}.png`)}
              style={{position: 'absolute', top: 0, left: 0, width: WIDTH}}
            />
          </Sequence>
        );
      })}
      {path === null ? null : <Cursor path={path} zoom={view.scale} />}
    </AbsoluteFill>
  );
};

const CORNERS: Record<Corner, CSSProperties> = {
  'bottom-left': {left: 64, bottom: 64},
  'bottom-right': {right: 64, bottom: 64},
  'top-right': {right: 64, top: 64},
};

const Caption = ({text, corner}: {text: string; corner: Corner}) => {
  const frame = useCurrentFrame();
  const shown = interpolate(frame, [0, CAPTION_IN_FRAMES], [0, 1], {
    easing: Easing.out(Easing.cubic),
    extrapolateRight: 'clamp',
  });
  return (
    <div
      style={{
        position: 'absolute',
        ...CORNERS[corner],
        padding: '20px 36px',
        borderRadius: 16,
        background: 'rgba(15, 18, 23, 0.92)',
        color: 'white',
        fontFamily,
        fontWeight: 800,
        fontSize: 60,
        letterSpacing: -1,
        opacity: shown,
        transform: `translateY(${(1 - shown) * 24}px)`,
      }}
    >
      {text}
    </div>
  );
};

/** A click sound on each press of the cursor in the scene. */
const Clicks = ({scene}: {scene: Scene}) => (
  <>
    {(cursorPathOf(scene)?.presses ?? []).map((press) => (
      <Sequence key={press} from={press} durationInFrames={CLICK_FRAMES} layout="none">
        <Audio src={staticFile('audio/click-tight.wav')} volume={scene.clickVolume ?? CLICK_VOLUME} />
      </Sequence>
    ))}
  </>
);

const SceneView = ({scene}: {scene: Scene}) => {
  const frame = useCurrentFrame();
  const opacity = interpolate(frame, [0, FADE_FRAMES], [0, 1], {extrapolateRight: 'clamp'});
  return (
    <AbsoluteFill style={{opacity, background: BACKGROUND}}>
      <Shots scene={scene} />
      <Caption text={scene.caption} corner={scene.corner} />
    </AbsoluteFill>
  );
};

const EndCard = () => {
  const frame = useCurrentFrame();
  const shown = interpolate(frame, [0, 15], [0, 1], {
    easing: Easing.out(Easing.cubic),
    extrapolateRight: 'clamp',
  });
  return (
    <AbsoluteFill
      style={{
        background: BACKGROUND,
        color: 'white',
        fontFamily,
        alignItems: 'center',
        justifyContent: 'center',
        gap: 16,
        opacity: shown,
      }}
    >
      <Img src={staticFile('logo.png')} style={{width: 220, height: 220}} />
      <div style={{fontWeight: 800, fontSize: 112, letterSpacing: -2}}>ILSight</div>
      <div style={{fontWeight: 500, fontSize: 44, color: '#c6ccd6'}}>A .NET assembly explorer.</div>
      <div style={{fontWeight: 800, fontSize: 44, marginTop: 28}}>Free and open source</div>
      <div style={{fontWeight: 500, fontSize: 36, color: '#7fb2ff'}}>github.com/Zeejfps/ILSight</div>
    </AbsoluteFill>
  );
};

const musicVolume = (frame: number): number =>
  MUSIC_VOLUME *
  interpolate(frame, [0, MUSIC_FADE_FRAMES, TOTAL_FRAMES - MUSIC_FADE_FRAMES, TOTAL_FRAMES], [0, 1, 1, 0], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

export type TrailerProps = {music: string};

export const Trailer = ({music}: TrailerProps) => {
  let start = 0;
  return (
    <AbsoluteFill style={{background: BACKGROUND}}>
      <Audio src={staticFile(`audio/${music}.mp3`)} volume={musicVolume} trimBefore={MUSIC_FIRST_BEAT} />
      {SCENES.map((scene) => {
        const from = start;
        const frames = sceneFrames(scene);
        start += frames;
        return (
          <Sequence key={scene.caption} from={from} durationInFrames={frames}>
            <SceneView scene={scene} />
            <Clicks scene={scene} />
          </Sequence>
        );
      })}
      <Sequence from={start} durationInFrames={END_CARD_FRAMES}>
        <EndCard />
      </Sequence>
    </AbsoluteFill>
  );
};
