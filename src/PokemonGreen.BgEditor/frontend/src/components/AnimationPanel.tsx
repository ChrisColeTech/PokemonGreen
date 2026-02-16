import { useEditorStore } from '../store/editorStore'

export default function AnimationPanel() {
  const animations = useEditorStore(s => s.animations)
  const animationPlaying = useEditorStore(s => s.animationPlaying)
  const activeClipIndex = useEditorStore(s => s.activeClipIndex)
  const setAnimationPlaying = useEditorStore(s => s.setAnimationPlaying)
  const setActiveClipIndex = useEditorStore(s => s.setActiveClipIndex)

  if (animations.length === 0) return null

  return (
    <div style={{
      display: 'flex',
      flexDirection: 'column',
      overflow: 'hidden',
      borderTop: '1px solid #2a2a4a',
    }}>
      {/* Header */}
      <div style={{
        padding: '10px 14px',
        borderBottom: '1px solid #2a2a4a',
        fontSize: 12,
        fontWeight: 600,
        color: '#ccc',
        flexShrink: 0,
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
      }}>
        <span>Animations</span>
        <span style={{ fontSize: 10, color: '#666', fontWeight: 400 }}>
          {animations.length} clip{animations.length !== 1 ? 's' : ''}
        </span>
      </div>

      {/* Content */}
      <div style={{
        flex: 1,
        overflow: 'auto',
        padding: '8px 14px',
      }}>
        {/* Play/Pause button */}
        <button
          onClick={() => setAnimationPlaying(!animationPlaying)}
          style={{
            width: '100%',
            padding: '8px 0',
            background: animationPlaying ? '#3a2a4a' : '#2a3a4a',
            border: '1px solid #3a3a6a',
            borderRadius: 4,
            color: '#ccc',
            fontSize: 12,
            cursor: 'pointer',
            marginBottom: 10,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 6,
          }}
        >
          {animationPlaying ? (
            <>
              {/* Pause icon */}
              <svg width="12" height="12" viewBox="0 0 12 12" fill="currentColor">
                <rect x="2" y="1" width="3" height="10" rx="0.5" />
                <rect x="7" y="1" width="3" height="10" rx="0.5" />
              </svg>
              Pause
            </>
          ) : (
            <>
              {/* Play icon */}
              <svg width="12" height="12" viewBox="0 0 12 12" fill="currentColor">
                <polygon points="2,1 10,6 2,11" />
              </svg>
              Play
            </>
          )}
        </button>

        {/* Clip list */}
        {animations.map((clip, i) => (
          <div
            key={clip.name + i}
            onClick={() => setActiveClipIndex(i)}
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              padding: '6px 10px',
              marginBottom: 3,
              borderRadius: 4,
              cursor: 'pointer',
              background: i === activeClipIndex ? '#2a2a5a' : 'transparent',
              border: i === activeClipIndex ? '1px solid #4a4a8a' : '1px solid transparent',
            }}
          >
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{
                fontSize: 11,
                color: i === activeClipIndex ? '#ddd' : '#aaa',
                whiteSpace: 'nowrap',
                overflow: 'hidden',
                textOverflow: 'ellipsis',
              }}>
                {clip.name || `Clip ${i}`}
              </div>
              <div style={{ fontSize: 9, color: '#666' }}>
                {clip.duration.toFixed(2)}s | {clip.tracks.length} tracks
              </div>
            </div>
            {i === activeClipIndex && (
              <div style={{
                width: 6,
                height: 6,
                borderRadius: '50%',
                background: animationPlaying ? '#6c8cff' : '#555',
                flexShrink: 0,
                marginLeft: 8,
              }} />
            )}
          </div>
        ))}
      </div>
    </div>
  )
}
