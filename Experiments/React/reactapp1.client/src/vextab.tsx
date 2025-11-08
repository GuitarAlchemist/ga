import {useEffect, useRef} from 'react';
import Vex from 'vexflow';

const VexTabDisplay = ({notation}) => {
    const divRef = useRef(null);

    useEffect(() => {
        if (divRef.current) {
            const VF = Vex.Flow;
            const renderer = new VF.Renderer(divRef.current, VF.Renderer.Backends.SVG);

            // Configure the rendering context.
            renderer.resize(500, 200);
            const context = renderer.getContext();
            context.setFont("Arial", 10, "").setBackgroundFillStyle("#eed");

            // Create a tab stave of width 400 at position 10, 40 on the canvas.
            const stave = new VF.TabStave(10, 40, 400);
            stave.addClef("tab").setContext(context).draw();

            // Create some tablature notes.
            const notes = [
                new VF.TabNote({positions: [{str: 3, fret: 7}], duration: "q"}),
                new VF.TabNote({positions: [{str: 2, fret: 8}], duration: "q"}),
                new VF.TabNote({positions: [{str: 3, fret: 9}], duration: "q"})
            ];

            // Create a voice in 4/4 and add the notes from above
            const voice = new VF.Voice({num_beats: 3, beat_value: 4});
            voice.addTickables(notes);

            // Format and justify the notes to 400 pixels.
            const formatter = new VF.Formatter().joinVoices([voice]).format([voice], 400);

            // Render voice
            voice.draw(context, stave);
        }
    }, [notation]);

    return (
        <div ref={divRef}></div>
    );
};

export default VexTabDisplay;
