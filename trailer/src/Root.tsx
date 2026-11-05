import {Composition} from 'remotion';
import {FPS, HEIGHT, TOTAL_FRAMES, WIDTH} from './timeline';
import {Trailer} from './Trailer';

export const Root = () => (
  <Composition
    id="Trailer"
    component={Trailer}
    durationInFrames={TOTAL_FRAMES}
    fps={FPS}
    width={WIDTH}
    height={HEIGHT}
    defaultProps={{music: 'option-c'}}
  />
);
