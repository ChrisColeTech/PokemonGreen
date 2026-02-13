export const pointKey = (x: number, y: number): string => `${x},${y}`

export const isInBounds = (x: number, y: number, width: number, height: number): boolean =>
  x >= 0 && y >= 0 && x < width && y < height
