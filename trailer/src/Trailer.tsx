import {loadFont} from '@remotion/google-fonts/Inter';
import type {CSSProperties} from 'react';
import {
  AbsoluteFill,
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
  SCENES,
  type Scene,
  type View,
  WIDTH,
  sceneFrames,
} from './timeline';

const {fontFamily} = loadFont('normal', {weights: ['500', '800'], subsets: ['latin']});

const BACKGROUND = '#1b1f26';
const FADE_FRAMES = 5;
const CAPTION_IN_FRAMES = 8;

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

const Shots = ({scene}: {scene: Scene}) => {
  const frame = useCurrentFrame();
  const duration = sceneFrames(scene);
  const progress = interpolate(frame, [0, duration - 1], [0, 1], {
    easing: Easing.bezier(0.45, 0, 0.2, 1),
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });
  const view: View = {
    scale: between(scene.from.scale, scene.to.scale, progress),
    x: between(scene.from.x, scene.to.x, progress),
    y: between(scene.from.y, scene.to.y, progress),
  };
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

export const Trailer = () => {
  let start = 0;
  return (
    <AbsoluteFill style={{background: BACKGROUND}}>
      {SCENES.map((scene) => {
        const from = start;
        const frames = sceneFrames(scene);
        start += frames;
        return (
          <Sequence key={scene.caption} from={from} durationInFrames={frames}>
            <SceneView scene={scene} />
          </Sequence>
        );
      })}
      <Sequence from={start} durationInFrames={END_CARD_FRAMES}>
        <EndCard />
      </Sequence>
    </AbsoluteFill>
  );
};
