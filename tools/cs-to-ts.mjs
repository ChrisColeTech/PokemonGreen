#!/usr/bin/env node
/**
 * C# to TypeScript converter for OhanaCli.
 *
 * Handles the mechanical translation patterns found in the OhanaCli codebase:
 * - Classes, enums, interfaces
 * - Type mappings (uint/int/byte/float/bool/string → number/boolean/string)
 * - BinaryReader → BinaryReader helper class
 * - List<T> → T[]
 * - Properties and fields
 * - Static methods
 * - Constructors
 * - Switch/case
 * - String formatting
 * - XML doc comments → stripped or converted
 *
 * Usage: node cs-to-ts.mjs <input.cs> [output.ts]
 *        node cs-to-ts.mjs --dir <inputDir> <outputDir>
 */

import fs from 'fs'
import path from 'path'

// ── Type mappings ──────────────────────────────────────────────────────────────

const TYPE_MAP = {
  // Primitives
  'uint': 'number',
  'int': 'number',
  'byte': 'number',
  'sbyte': 'number',
  'short': 'number',
  'ushort': 'number',
  'long': 'number',
  'ulong': 'number',
  'float': 'number',
  'double': 'number',
  'decimal': 'number',
  'bool': 'boolean',
  'string': 'string',
  'char': 'string',
  'void': 'void',
  'object': 'any',
  'var': 'let',

  // .NET types
  'String': 'string',
  'Int32': 'number',
  'UInt32': 'number',
  'Int16': 'number',
  'UInt16': 'number',
  'Byte': 'number',
  'Single': 'number',
  'Double': 'number',
  'Boolean': 'boolean',

  // Collections
  'Bitmap': 'ImageData',
  'Color': 'number',
  'Stream': 'BinaryReader',
  'MemoryStream': 'Buffer',
  'FileStream': 'Buffer',
}

// ── Main converter ─────────────────────────────────────────────────────────────

function convertCsharpToTypescript(csCode, fileName) {
  let ts = csCode

  // Track namespace/class context for import generation
  const imports = new Set()

  // ── Pass 1: Remove/simplify C# boilerplate ──────────────────────────────────

  // Remove using statements (we'll generate imports later)
  ts = ts.replace(/^using [^;]+;\s*\n/gm, '')

  // Remove namespace declarations but keep content
  ts = ts.replace(/namespace\s+[\w.]+\s*\{/g, '// namespace')
  // We'll handle the closing brace later

  // Remove XML doc comments (/// <summary> blocks)
  ts = ts.replace(/\s*\/\/\/\s*<[^>]+>.*$/gm, '')
  ts = ts.replace(/\s*\/\/\/\s*.*$/gm, '')

  // Remove [Attribute] decorators
  ts = ts.replace(/^\s*\[[\w.(),"= ]+\]\s*$/gm, '')

  // ── Pass 2: Class/struct/enum declarations ──────────────────────────────────

  // public class Foo : Bar → export class Foo extends Bar
  ts = ts.replace(
    /(?:public|internal|private)?\s*(?:static\s+)?(?:partial\s+)?class\s+(\w+)(?:\s*:\s*([\w.,\s<>]+))?/g,
    (_, name, bases) => {
      if (bases) {
        // Split base classes, first is extends, rest are implements
        const baseList = bases.split(',').map(b => b.trim())
        const ext = baseList[0]
        return `export class ${name} extends ${mapType(ext)}`
      }
      return `export class ${name}`
    }
  )

  // public struct Foo → export class Foo (structs become classes in TS)
  ts = ts.replace(
    /(?:public|internal|private)?\s*struct\s+(\w+)/g,
    'export class $1'
  )

  // public enum Foo → export enum Foo
  ts = ts.replace(
    /(?:public|internal|private)?\s*enum\s+(\w+)/g,
    'export enum $1'
  )

  // public interface Foo → export interface Foo
  ts = ts.replace(
    /(?:public|internal|private)?\s*interface\s+(\w+)/g,
    'export interface $1'
  )

  // ── Pass 3: Field declarations ──────────────────────────────────────────────

  // public float x; → x: number = 0
  // public List<OBone> skeleton = new List<OBone>(); → skeleton: OBone[] = []
  ts = ts.replace(
    /^(\s*)(?:public|private|protected|internal)\s+(?:static\s+)?(?:readonly\s+)?(List<(\w+)>)\s+(\w+)\s*=\s*new\s+List<\w+>\(\)\s*;/gm,
    (_, indent, _full, innerType, name) => {
      return `${indent}${name}: ${mapType(innerType)}[] = []`
    }
  )

  // public List<T> foo; → foo: T[] = []
  ts = ts.replace(
    /^(\s*)(?:public|private|protected|internal)\s+(?:static\s+)?(?:readonly\s+)?List<(\w+)>\s+(\w+)\s*;/gm,
    (_, indent, innerType, name) => {
      return `${indent}${name}: ${mapType(innerType)}[] = []`
    }
  )

  // public Type name = value; → name: Type = value
  ts = ts.replace(
    /^(\s*)(?:public|private|protected|internal)\s+(?:static\s+)?(?:readonly\s+)?(?:const\s+)?(\w+(?:<[\w,\s]+>)?(?:\[\])?)\s+(\w+)\s*=\s*(.+);/gm,
    (_, indent, type, name, value) => {
      const tsType = mapType(type)
      const tsValue = convertValue(value.trim(), type)
      return `${indent}${name}: ${tsType} = ${tsValue}`
    }
  )

  // public Type name; → name: Type (with default for primitives)
  ts = ts.replace(
    /^(\s*)(?:public|private|protected|internal)\s+(?:static\s+)?(?:readonly\s+)?(\w+(?:<[\w,\s]+>)?(?:\[\])?(?:\?)?)\s+(\w+)\s*;/gm,
    (_, indent, type, name) => {
      const tsType = mapType(type)
      const def = defaultFor(tsType)
      return `${indent}${name}: ${tsType}${def ? ` = ${def}` : ''}`
    }
  )

  // ── Pass 4: Method signatures ───────────────────────────────────────────────

  // public static ReturnType methodName(params) → static methodName(params): ReturnType
  ts = ts.replace(
    /^(\s*)(?:public|private|protected|internal)\s+(static\s+)?(?:override\s+)?(\w+(?:<[\w,\s]+>)?(?:\[\])?)\s+(\w+)\s*\(([^)]*)\)/gm,
    (_, indent, isStatic, returnType, name, params) => {
      const tsReturn = mapType(returnType)
      const tsParams = convertParams(params)
      const stat = isStatic ? 'static ' : ''
      return `${indent}${stat}${name}(${tsParams}): ${tsReturn}`
    }
  )

  // Constructor: public ClassName(params) → constructor(params)
  // Match constructors by finding methods where name matches a known class pattern
  ts = ts.replace(
    /^(\s*)(?:public|private|protected|internal)\s+(\w+)\s*\(([^)]*)\)\s*$/gm,
    (match, indent, name, params) => {
      // If name starts with uppercase and there's no return type, it's likely a constructor
      if (name[0] === name[0].toUpperCase() && !name.match(/^(If|For|While|Switch|Return|Get|Set)$/)) {
        const tsParams = convertParams(params)
        return `${indent}constructor(${tsParams})`
      }
      return match
    }
  )

  // ── Pass 5: Common C# → TS expression transforms ───────────────────────────

  // new ClassName() → new ClassName()  (mostly same, but handle some types)
  ts = ts.replace(/new\s+List<(\w+)>\s*\(\)/g, (_, t) => `[] as ${mapType(t)}[]`)
  ts = ts.replace(/new\s+Dictionary<(\w+),\s*(\w+)>\s*\(\)/g,
    (_, k, v) => `new Map<${mapType(k)}, ${mapType(v)}>()`)

  // (type)value → value as type  OR  Number(value) for numeric casts
  // Only handle simple casts, not complex expressions
  ts = ts.replace(/\((?:uint|int|byte|ushort|short|float|double|long|ulong)\)\s*(\w+)/g, '$1')

  // array.Length → array.length
  ts = ts.replace(/\.Length\b/g, '.length')

  // string.Empty → ''
  ts = ts.replace(/string\.Empty/g, "''")

  // string.Format("...", args) → simple template or format helper
  ts = ts.replace(/string\.Format\("([^"]+)"((?:,\s*[^)]+)*)\)/g,
    (_, fmt, args) => {
      if (!args) return `'${fmt}'`
      const argList = args.replace(/^\s*,\s*/, '').split(/\s*,\s*/)
      let result = fmt
      argList.forEach((arg, i) => {
        // Replace {0}, {1}, {0:D5}, etc with ${arg}
        const pattern = new RegExp(`\\{${i}(?::[^}]+)?\\}`, 'g')
        result = result.replace(pattern, `\${${arg.trim()}}`)
      })
      return '`' + result + '`'
    }
  )

  // Math.X → Math.X (mostly same)
  // Console.Error.WriteLine → console.error
  ts = ts.replace(/Console\.Error\.WriteLine/g, 'console.error')
  ts = ts.replace(/Console\.WriteLine/g, 'console.log')

  // input.ReadUInt32() → input.readUInt32()
  ts = ts.replace(/\.ReadUInt32\(\)/g, '.readUInt32()')
  ts = ts.replace(/\.ReadInt32\(\)/g, '.readInt32()')
  ts = ts.replace(/\.ReadUInt16\(\)/g, '.readUInt16()')
  ts = ts.replace(/\.ReadInt16\(\)/g, '.readInt16()')
  ts = ts.replace(/\.ReadByte\(\)/g, '.readByte()')
  ts = ts.replace(/\.ReadSByte\(\)/g, '.readSByte()')
  ts = ts.replace(/\.ReadSingle\(\)/g, '.readFloat()')
  ts = ts.replace(/\.ReadBoolean\(\)/g, '.readBoolean()')
  ts = ts.replace(/\.Read\((\w+),\s*(\d+),\s*(\w+(?:\.\w+)*)\)/g, '.readBytes($1, $2, $3)')

  // input.BaseStream.Seek(offset, SeekOrigin.Begin) → input.seek(offset)
  ts = ts.replace(/(\w+)\.BaseStream\.Seek\(([^,]+),\s*SeekOrigin\.Begin\)/g, '$1.seek($2)')
  ts = ts.replace(/(\w+)\.BaseStream\.Seek\(([^,]+),\s*SeekOrigin\.Current\)/g, '$1.seekRelative($2)')
  ts = ts.replace(/(\w+)\.BaseStream\.Position/g, '$1.position')

  // data.Seek(offset, SeekOrigin.Begin) → data.seek(offset) (for Stream params)
  ts = ts.replace(/(\w+)\.Seek\(([^,]+),\s*SeekOrigin\.Begin\)/g, '$1.seek($2)')
  ts = ts.replace(/(\w+)\.Seek\(([^,]+),\s*SeekOrigin\.Current\)/g, '$1.seekRelative($2)')
  ts = ts.replace(/(\w+)\.Position\b/g, (match, name) => {
    // Only convert .Position for stream-like variables, not arbitrary objects
    if (['data', 'input', 'stream', 'ms'].includes(name)) {
      return `${name}.position`
    }
    return match
  })

  // data.Close() → (remove, not needed in TS)
  ts = ts.replace(/^\s*\w+\.Close\(\)\s*;?\s*$/gm, '')

  // new BinaryReader(data) → new BinaryReader(data) (keep, we'll provide this class)
  // new MemoryStream(buffer) → BinaryReader.fromBuffer(buffer)
  ts = ts.replace(/new\s+MemoryStream\((\w+)\)/g, 'BinaryReader.fromBuffer($1)')
  ts = ts.replace(/new\s+BinaryReader\((\w+)\)/g, '$1')

  // byte[] → Buffer
  ts = ts.replace(/\bbyte\[\]/g, 'Buffer')
  ts = ts.replace(/\buint\[\]/g, 'number[]')
  ts = ts.replace(/\bint\[\]/g, 'number[]')
  ts = ts.replace(/\bfloat\[\]/g, 'number[]')
  ts = ts.replace(/\bushort\[\]/g, 'number[]')
  ts = ts.replace(/\bshort\[\]/g, 'number[]')

  // new byte[size] → Buffer.alloc(size)
  ts = ts.replace(/new\s+byte\[([^\]]+)\]/g, 'Buffer.alloc($1)')
  ts = ts.replace(/new\s+(?:uint|int|float|ushort|short)\[([^\]]+)\]/g, 'new Array<number>($1).fill(0)')

  // Remaining type keywords in variable declarations
  ts = ts.replace(/\b(uint|int|byte|sbyte|ushort|short|long|ulong|float|double)\s+(\w+)\s*=/g,
    'let $2 =')
  ts = ts.replace(/\b(uint|int|byte|sbyte|ushort|short|long|ulong|float|double)\s+(\w+)\s*;/g,
    'let $2 = 0;')
  ts = ts.replace(/\bbool\s+(\w+)\s*=/g, 'let $1 =')
  ts = ts.replace(/\bbool\s+(\w+)\s*;/g, 'let $1 = false;')
  ts = ts.replace(/\bstring\s+(\w+)\s*=/g, 'let $1 =')
  ts = ts.replace(/\bstring\s+(\w+)\s*;/g, "let $1 = '';")

  // for (int i = 0 → for (let i = 0
  ts = ts.replace(/for\s*\(\s*(?:int|uint|long|ulong)\s+/g, 'for (let ')

  // foreach (Type item in collection) → for (const item of collection)
  ts = ts.replace(
    /foreach\s*\(\s*(?:var|[\w.<>[\]]+)\s+(\w+)\s+in\s+(\w+(?:\.\w+)*)\)/g,
    'for (const $1 of $2)'
  )

  // .Add( → .push(
  ts = ts.replace(/\.Add\(/g, '.push(')
  // .AddRange( → .push(...
  ts = ts.replace(/\.AddRange\(([^)]+)\)/g, '.push(...$1)')
  // .Count → .length
  ts = ts.replace(/\.Count\b/g, '.length')
  // .Contains( → .includes(
  ts = ts.replace(/\.Contains\(/g, '.includes(')
  // .Remove( → (manual, leave as is)
  // .Clear() → .length = 0 or = []
  ts = ts.replace(/(\w+)\.Clear\(\)/g, '$1.length = 0')

  // Encoding.ASCII.GetString(data, startIndex, length)
  ts = ts.replace(
    /Encoding\.ASCII\.GetString\((\w+),\s*(\w+),\s*(\w+)\)/g,
    '$1.toString(\'ascii\', $2, $2 + $3)'
  )

  // BitConverter.ToSingle(buffer, offset)
  ts = ts.replace(
    /BitConverter\.ToSingle\((\w+),\s*(\w+)\)/g,
    '$1.readFloatLE($2)'
  )
  ts = ts.replace(
    /BitConverter\.ToUInt32\((\w+),\s*(\w+)\)/g,
    '$1.readUInt32LE($2)'
  )
  ts = ts.replace(
    /BitConverter\.ToInt32\((\w+),\s*(\w+)\)/g,
    '$1.readInt32LE($2)'
  )

  // Buffer.BlockCopy(src, srcOff, dst, dstOff, count) → src.copy(dst, dstOff, srcOff, srcOff + count)
  ts = ts.replace(
    /Buffer\.BlockCopy\((\w+),\s*(\w+),\s*(\w+),\s*(\w+),\s*(\w+)\)/g,
    '$1.copy($3, $4, $2, $2 + $5)'
  )

  // Remove access modifiers from remaining places
  ts = ts.replace(/\bpublic\s+/g, '')
  ts = ts.replace(/\bprivate\s+/g, '')
  ts = ts.replace(/\bprotected\s+/g, '')
  ts = ts.replace(/\binternal\s+/g, '')

  // Remove 'virtual', 'override', 'sealed', 'readonly', 'partial'
  ts = ts.replace(/\bvirtual\s+/g, '')
  ts = ts.replace(/\boverride\s+/g, '')
  ts = ts.replace(/\bsealed\s+/g, '')
  ts = ts.replace(/\breadonly\s+/g, '')
  ts = ts.replace(/\bpartial\s+/g, '')

  // ── Pass 6: Clean up ────────────────────────────────────────────────────────

  // Remove trailing namespace closing brace (outermost)
  // This is tricky — for now just leave it

  // Numeric suffixes: 0.5f → 0.5, 1000u → 1000
  ts = ts.replace(/(\d+(?:\.\d+)?)f\b/g, '$1')
  ts = ts.replace(/(\d+)u\b/g, '$1')
  ts = ts.replace(/(\d+)L\b/g, '$1')

  // Add file header
  const header = `// Auto-converted from ${fileName}\n// Manual review required for: BinaryReader usage, Buffer ops, class hierarchy\n\n`

  return header + ts
}

// ── Helper functions ───────────────────────────────────────────────────────────

function mapType(csType) {
  if (!csType) return 'any'
  csType = csType.trim()

  // Handle nullable
  if (csType.endsWith('?')) {
    return mapType(csType.slice(0, -1)) + ' | null'
  }

  // Handle arrays
  if (csType.endsWith('[]')) {
    return mapType(csType.slice(0, -2)) + '[]'
  }

  // Handle generics: List<T> → T[], Dictionary<K,V> → Map<K,V>
  const genericMatch = csType.match(/^(\w+)<(.+)>$/)
  if (genericMatch) {
    const outer = genericMatch[1]
    const inner = genericMatch[2]
    if (outer === 'List') return mapType(inner) + '[]'
    if (outer === 'Dictionary') {
      const parts = inner.split(',').map(p => mapType(p.trim()))
      return `Map<${parts.join(', ')}>`
    }
    return `${outer}<${inner.split(',').map(p => mapType(p.trim())).join(', ')}>`
  }

  // Direct type map
  if (TYPE_MAP[csType]) return TYPE_MAP[csType]

  // Keep other types as-is (likely project types like OVector3, OBone, etc.)
  return csType
}

function convertParams(params) {
  if (!params || !params.trim()) return ''
  return params.split(',').map(p => {
    p = p.trim()
    if (!p) return ''

    // Handle default values: Type name = default
    const defMatch = p.match(/^([\w.<>\[\]?]+)\s+(\w+)\s*=\s*(.+)$/)
    if (defMatch) {
      const [, type, name, def] = defMatch
      return `${name}: ${mapType(type)} = ${convertValue(def.trim(), type)}`
    }

    // Handle ref/out params
    p = p.replace(/\b(ref|out|in|params)\s+/, '')

    const parts = p.match(/^([\w.<>\[\]?]+)\s+(\w+)$/)
    if (parts) {
      return `${parts[2]}: ${mapType(parts[1])}`
    }
    return p
  }).join(', ')
}

function convertValue(value, type) {
  if (value === 'null') return 'null'
  if (value === 'true' || value === 'false') return value
  if (value === 'string.Empty') return "''"

  // Numeric with suffix
  value = value.replace(/(\d+(?:\.\d+)?)f$/i, '$1')
  value = value.replace(/(\d+)u$/i, '$1')
  value = value.replace(/(\d+)L$/i, '$1')

  // new Type() → new Type()
  value = value.replace(/new\s+List<(\w+)>\s*\(\)/, `[] as ${mapType('$1')}[]`)

  return value
}

function defaultFor(tsType) {
  if (tsType === 'number') return '0'
  if (tsType === 'boolean') return 'false'
  if (tsType === 'string') return "''"
  if (tsType.endsWith('[]')) return '[]'
  if (tsType.endsWith('| null')) return 'null'
  return null
}

// ── CLI ────────────────────────────────────────────────────────────────────────

const args = process.argv.slice(2)

if (args[0] === '--dir') {
  // Batch mode: convert all .cs files in a directory tree
  const inputDir = args[1]
  const outputDir = args[2]

  if (!inputDir || !outputDir) {
    console.error('Usage: node cs-to-ts.mjs --dir <inputDir> <outputDir>')
    process.exit(1)
  }

  function walkDir(dir) {
    const entries = fs.readdirSync(dir, { withFileTypes: true })
    const files = []
    for (const entry of entries) {
      const full = path.join(dir, entry.name)
      if (entry.isDirectory()) {
        files.push(...walkDir(full))
      } else if (entry.name.endsWith('.cs')) {
        files.push(full)
      }
    }
    return files
  }

  const csFiles = walkDir(inputDir)
  let converted = 0

  for (const csFile of csFiles) {
    const relPath = path.relative(inputDir, csFile)
    const tsPath = path.join(outputDir, relPath.replace(/\.cs$/, '.ts'))

    const dir = path.dirname(tsPath)
    fs.mkdirSync(dir, { recursive: true })

    const csCode = fs.readFileSync(csFile, 'utf-8')
    const tsCode = convertCsharpToTypescript(csCode, relPath)
    fs.writeFileSync(tsPath, tsCode)

    console.log(`  ${relPath} → ${path.relative(outputDir, tsPath)}`)
    converted++
  }

  console.log(`\nConverted ${converted} files to ${outputDir}`)
} else if (args[0] === '--help' || args.length === 0) {
  console.log('C# to TypeScript converter for OhanaCli')
  console.log('')
  console.log('Usage:')
  console.log('  node cs-to-ts.mjs <input.cs> [output.ts]    Convert single file')
  console.log('  node cs-to-ts.mjs --dir <inputDir> <outDir> Convert directory tree')
} else {
  // Single file mode
  const inputFile = args[0]
  const outputFile = args[1] || inputFile.replace(/\.cs$/, '.ts')

  const csCode = fs.readFileSync(inputFile, 'utf-8')
  const tsCode = convertCsharpToTypescript(csCode, path.basename(inputFile))
  fs.writeFileSync(outputFile, tsCode)

  console.log(`Converted ${inputFile} → ${outputFile}`)
}
