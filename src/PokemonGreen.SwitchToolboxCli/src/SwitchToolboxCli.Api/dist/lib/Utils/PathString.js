import * as path from 'path';
export class PathString {
    rootPath;
    constructor(root) {
        this.rootPath = path.dirname(root);
    }
    combine(str) {
        return path.join(this.rootPath, str);
    }
}
//# sourceMappingURL=PathString.js.map