import { useEditorStore } from '../store/editorStore'
import Viewport from '../components/Viewport'
import { Film, Pause, Play } from 'lucide-react'

export default function AnimationsPage() {
  const scene = useEditorStore(s => s.scene)
  const sceneName = useEditorStore(s => s.sceneName)
  const animations = useEditorStore(s => s.animations)
  const animationPlaying = useEditorStore(s => s.animationPlaying)
  const activeClipIndex = useEditorStore(s => s.activeClipIndex)
  const setAnimationPlaying = useEditorStore(s => s.setAnimationPlaying)
  const setActiveClipIndex = useEditorStore(s => s.setActiveClipIndex)

  if (!scene) {
    return (
      <div style={{
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        alignItems: 'center',
        justifyContent: 'center',
        color: '#666',
        fontSize: 14,
        gap: 8,
      }}>
        <Film size={32} strokeWidth={1.5} />
        <span>No model loaded</span>
        <span style={{ fontSize: 12 }}>Load a model from the Tools page first</span>
      </div>
    )
  }

  return (
    <div style={{
      display: 'flex',
      flexDirection: 'column',
      height: '100%',
      overflow: 'hidden',
    }}>
      {/* Header */}
      <div style={{
        padding: '12px 20px',
        background: '#12122a',
        borderBottom: '1px solid #2a2a4a',
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        flexShrink: 0,
      }}>
        <h1 style={{ margin: 0, fontSize: 16, color: '#e0e0e0' }}>Animations</h1>
        {sceneName && (
          <span style={{ fontSize: 12, color: '#888' }}>{sceneName}</span>
        )}
      </div>

      {/* Main area: viewport + panel */}
      <div style={{
        flex: 1,
        display: 'flex',
        overflow: 'hidden',
      }}>
        {/* Viewport */}
        <Viewport />

        {/* Animation panel */}
        <div style={{
          width: 300,
          display: 'flex',
          flexDirection: 'column',
          background: '#16162a',
          borderLeft: '1px solid #2a2a4a',
          overflow: 'hidden',
        }}>
          {/* Controls */}
          <div style={{
            padding: '12px 16px',
            borderBottom: '1px solid #2a2a4a',
            display: 'flex',
            alignItems: 'center',
            gap: 10,
            flexShrink: 0,
          }}>
            <button
              onClick={() => setAnimationPlaying(!animationPlaying)}
              style={{
                padding: '6px 16px',
                background: animationPlaying ? '#3a2a4a' : '#2a3a4a',
                border: '1px solid #3a3a6a',
                borderRadius: 4,
                color: '#ccc',
                fontSize: 12,
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                flexShrink: 0,
              }}
            >
              {animationPlaying ? (
                <>
                  <Pause size={12} strokeWidth={2} />
                  Pause
                </>
              ) : (
                <>
                  <Play size={12} strokeWidth={2} />
                  Play
                </>
              )}
            </button>
            <span style={{ fontSize: 12, color: '#888' }}>
              {animations.length > 0
                ? `${activeClipIndex + 1} / ${animations.length}`
                : 'No clips'}
            </span>
          </div>

          {/* Clip list */}
          <div style={{
            flex: 1,
            overflowY: 'auto',
            padding: '8px 0',
          }}>
            {animations.length === 0 ? (
              <div style={{ padding: '20px 16px', color: '#666', fontSize: 12, textAlign: 'center' }}>
                No animation clips found
              </div>
            ) : (
              animations.map((clip, i) => (
                <div
                  key={clip.name + i}
                  onClick={() => setActiveClipIndex(i)}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    padding: '8px 16px',
                    cursor: 'pointer',
                    background: i === activeClipIndex ? '#2a2a5a' : 'transparent',
                    borderLeft: i === activeClipIndex ? '3px solid #8c8cff' : '3px solid transparent',
                  }}
                  onMouseEnter={e => {
                    if (i !== activeClipIndex) e.currentTarget.style.background = '#1e1e3a'
                  }}
                  onMouseLeave={e => {
                    if (i !== activeClipIndex) e.currentTarget.style.background = 'transparent'
                  }}
                >
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{
                      fontSize: 12,
                      color: i === activeClipIndex ? '#e0e0e0' : '#aaa',
                      whiteSpace: 'nowrap',
                      overflow: 'hidden',
                      textOverflow: 'ellipsis',
                    }}>
                      {clip.name || `Clip ${i}`}
                    </div>
                    <div style={{ fontSize: 10, color: '#666', marginTop: 2 }}>
                      {clip.duration.toFixed(2)}s &middot; {clip.tracks.length} tracks
                    </div>
                  </div>
                  {i === activeClipIndex && (
                    <div style={{
                      width: 8,
                      height: 8,
                      borderRadius: '50%',
                      background: animationPlaying ? '#8c8cff' : '#555',
                      flexShrink: 0,
                      marginLeft: 10,
                    }} />
                  )}
                </div>
              ))
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
