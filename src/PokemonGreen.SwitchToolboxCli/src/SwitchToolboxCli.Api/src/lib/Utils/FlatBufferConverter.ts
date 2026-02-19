import * as fs from 'fs';
import * as flatbuffers from 'flatbuffers';

export class FlatBufferConverter {
    static DeserializeFrom(data: string | Buffer, RootType: any): any {
        const buf = typeof data === 'string' ? fs.readFileSync(data) : data;
        const bb = new flatbuffers.ByteBuffer(new Uint8Array(buf));
        const rootMethod = `getRootAs${RootType.name}`;
        return (RootType as any)[rootMethod](bb);
    }
}