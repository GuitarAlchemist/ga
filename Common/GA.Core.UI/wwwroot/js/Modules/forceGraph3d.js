/**
 * Initializes a force graph 3d
 * @param {any} element // The html element for the force graph 3d
 */
export async function forceGraph3dInit(element, libUrl) {
    if (!element) {
        console.log('No force graph 3d element provided');
        return false;
    }

    await import(libUrl);

    try {
        const gData = {
            nodes: [...Array(9).keys()].map(i => ({id: i})),
            links: [
                {source: 1, target: 4, curvature: 0},
                {source: 1, target: 4, curvature: 0.5},
                {source: 1, target: 4, curvature: -0.5},
                {source: 5, target: 2, curvature: 0.3},
                {source: 2, target: 5, curvature: 0.3},
                {source: 0, target: 3, curvature: 0},
                {source: 3, target: 3, curvature: 0.5},
                {source: 0, target: 4, curvature: 0.2},
                {source: 4, target: 5, curvature: 0.5},
                {source: 5, target: 6, curvature: 0.7},
                {source: 6, target: 7, curvature: 1},
                {source: 7, target: 8, curvature: 2},
                {source: 8, target: 0, curvature: 0.5}
            ]
        };

        const _ = ForceGraph3D()
        (element)
            .linkDirectionalParticles(2)
            .linkCurvature('curvature')
            .linkDirectionalParticleColor(() => 'red')
            .linkDirectionalParticleWidth(2)
            .linkHoverPrecision(10)
            .graphData(gData);
    } catch (e) {
        console.error(e);
    }
}
