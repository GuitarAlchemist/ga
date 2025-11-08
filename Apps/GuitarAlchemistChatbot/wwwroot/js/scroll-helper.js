// Scroll helper functions for infinite scrolling
window.getScrollInfo = function (element) {
    return {
        scrollTop: element.scrollTop,
        scrollHeight: element.scrollHeight,
        clientHeight: element.clientHeight
    };
};

