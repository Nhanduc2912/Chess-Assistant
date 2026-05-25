from PIL import Image
import os

src = r'C:\Users\admin\.gemini\antigravity\brain\ab489e16-1a5c-4796-a88a-25f2fd19add9\chess_icon_1777423823606.png'
out = r'D:\WEB\Chess-assistant\ChessAssistantRoot\chrome-extension\icons'

img = Image.open(src).convert('RGBA')
for size in [16, 48, 128]:
    img.resize((size, size), Image.LANCZOS).save(os.path.join(out, f'icon{size}.png'))
    print(f'Saved icon{size}.png')
