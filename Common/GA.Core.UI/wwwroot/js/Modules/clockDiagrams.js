export function init(element) {
    if (!element) {
        console.log('No element provided');
        return false;
    }

    // Make an instance of two and place it on the page.
    const params = {
        fullscreen: true
    };
    const elem = document.body;
    const two = new Two(params).appendTo(elem);

    // Two.js has convenient methods to make shapes and insert them into the scene.
    const radius = 50;
    const x = two.width * 0.5;
    var y = two.height * 0.5 - radius * 1.25;
    const circle = two.makeCircle(x, y, radius);

    y = two.height * 0.5 + radius * 1.25;
    const width = 100;
    const height = 100;
    const rect = two.makeRectangle(x, y, width, height);

    // The object returned has many stylable properties:
    circle.fill = '#FF8000';
    // And accepts all valid CSS color:
    circle.stroke = 'orangered';
    circle.linewidth = 5;

    rect.fill = 'rgb(0, 200, 255)';
    rect.opacity = 0.75;
    rect.noStroke();

    // Don’t forget to tell two to draw everything to the screen
    two.update();
}
