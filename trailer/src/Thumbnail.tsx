import {loadFont} from '@remotion/google-fonts/Inter';
import {AbsoluteFill, Img, staticFile} from 'remotion';

const {fontFamily} = loadFont('normal', {weights: ['800'], subsets: ['latin']});

const BACKGROUND = '#12151b';
const ACCENT = '#7fb2ff';
const SHOT_WIDTH = 1180;

/** The YouTube thumbnail: the name and promise at the left, big enough to read at a small size, over
 * the real app at the right, showing the Analyzer on real code. */
export const Thumbnail = () => (
  <AbsoluteFill style={{background: BACKGROUND, overflow: 'hidden', fontFamily}}>
    <Img
      src={staticFile('frames/analyzer/00009.png')}
      style={{
        position: 'absolute',
        left: 470,
        top: 96,
        width: SHOT_WIDTH,
        borderRadius: 18,
        boxShadow: '0 24px 70px rgba(0, 0, 0, 0.75)',
        outline: '2px solid rgba(255, 255, 255, 0.12)',
      }}
    />
    <AbsoluteFill
      style={{
        background: `linear-gradient(90deg, ${BACKGROUND} 42%, rgba(18, 21, 27, 0.55) 62%, rgba(18, 21, 27, 0) 80%)`,
      }}
    />
    <div
      style={{
        position: 'absolute',
        left: 60,
        top: 0,
        bottom: 0,
        width: 680,
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'center',
        color: 'white',
      }}
    >
      <div style={{display: 'flex', alignItems: 'center', gap: 20}}>
        <Img src={staticFile('logo.png')} style={{width: 112, height: 112}} />
        <div style={{fontWeight: 800, fontSize: 92, letterSpacing: -3}}>ILSight</div>
      </div>
      <div style={{fontWeight: 800, fontSize: 84, lineHeight: 1.04, letterSpacing: -3, marginTop: 30}}>
        See inside any <span style={{color: ACCENT}}>.NET</span> assembly
      </div>
    </div>
  </AbsoluteFill>
);
