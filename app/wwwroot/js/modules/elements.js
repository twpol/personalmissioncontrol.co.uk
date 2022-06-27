export function getElements() {
    const e = Object.create(null);
    for (const element of document.querySelectorAll("[id]")) {
        setByPath(e, element.id.split("-"), element);
    }
    return e;
}

function setByPath(root, path, value) {
    for (let i = 0; i < path.length - 1; i++) {
        root[path[i]] = root[path[i]] || Object.create(null);
        root = root[path[i]];
    }
    root[path[path.length - 1]] = value;
}

export function $(name, ...children) {
    const element = document.createElement(name);
    if (children.length && Object.getPrototypeOf(children[0]) === Object.prototype) {
        const attributes = children.shift();
        for (const attribute of Object.keys(attributes)) {
            if (typeof attributes[attribute] !== "undefined") {
                element.setAttribute(attribute, attributes[attribute]);
            }
        }
    }
    element.append(...children);
    return element;
}
