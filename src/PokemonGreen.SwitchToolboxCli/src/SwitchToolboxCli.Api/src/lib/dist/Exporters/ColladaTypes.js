/**
 * COLLADA XML DOM types for building DAE documents.
 * Ported from C# ColladaTypes.cs
 */
export var SemanticType;
(function (SemanticType) {
    SemanticType["POSITION"] = "POSITION";
    SemanticType["VERTEX"] = "VERTEX";
    SemanticType["NORMAL"] = "NORMAL";
    SemanticType["TEXCOORD"] = "TEXCOORD";
    SemanticType["COLOR"] = "COLOR";
    SemanticType["WEIGHT"] = "WEIGHT";
    SemanticType["JOINT"] = "JOINT";
    SemanticType["INV_BIND_MATRIX"] = "INV_BIND_MATRIX";
})(SemanticType || (SemanticType = {}));
// XML helper functions
export function attr(name, value) {
    return ` ${name}="${escapeXml(value)}"`;
}
export function escapeXml(text) {
    return text
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}
export function fmtFloat(f) {
    return f.toFixed(6).replace(/\.?0+$/, '');
}
export function fmtMatrix(_m) {
    // Column-major for COLLADA
    // Matrix4 is a stub - would format actual matrix values
    // For now, return identity matrix
    return '1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1';
}
export function createElement(name, attributes, content) {
    const attrs = Object.entries(attributes)
        .map(([k, v]) => attr(k, v))
        .join('');
    if (content !== undefined) {
        return `<${name}${attrs}>${content}</${name}>`;
    }
    return `<${name}${attrs}/>`;
}
export function createElementWithChildren(name, attributes, children) {
    const attrs = Object.entries(attributes)
        .map(([k, v]) => attr(k, v))
        .join('');
    return `<${name}${attrs}>\n${children.map(c => '  ' + c.replace(/\n/g, '\n  ')).join('\n')}\n</${name}>`;
}
export const COLLADA_NAMESPACE = 'http://www.collada.org/2005/11/COLLADASchema';
export const COLLADA_VERSION = '1.4.1';
//# sourceMappingURL=ColladaTypes.js.map