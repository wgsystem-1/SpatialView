# SpatialView - Design System (디자인 시스템)

---

## 1. Design Philosophy (디자인 철학)

### 1.1 Core Principles (핵심 원칙)

| Principle | Description |
|-----------|-------------|
| **Flat & Colorful** | Flat Design에 밝은 색상으로 친근하고 현대적인 느낌 |
| **Icon-Centric** | 아이콘 중심의 직관적 인터페이스 |
| **Minimal Complexity** | QGIS 대비 Simple UI, 핵심 기능만 전면에 |
| **Professional Yet Friendly** | 전문가 도구이지만 접근하기 쉬운 디자인 |

### 1.2 Design Reference (디자인 레퍼런스)

- **Slack** - 밝은 색상, 아이콘 중심 UI
- **Trello** - Flat Colorful, 직관적 카드 레이아웃
- **Figma** - 전문 도구의 현대적 UI

---

## 2. Color Palette (색상 팔레트)

### 2.1 Primary Colors (주요 색상)

| Name | Hex | RGB | Usage |
|------|-----|-----|-------|
| **Primary** | `#2196F3` | 33, 150, 243 | 주요 버튼, 선택 상태, Link |
| **Primary Dark** | `#1976D2` | 25, 118, 210 | Hover 상태, 강조 |
| **Primary Light** | `#BBDEFB` | 187, 222, 251 | 배경 Highlight |

### 2.2 Secondary Colors (보조 색상)

| Name | Hex | RGB | Usage |
|------|-----|-----|-------|
| **Secondary** | `#FF9800` | 255, 152, 0 | 알림, 경고, Accent |
| **Secondary Dark** | `#F57C00` | 245, 124, 0 | Hover, 강조 |

### 2.3 Semantic Colors (의미적 색상)

| Name | Hex | RGB | Usage |
|------|-----|-----|-------|
| **Success** | `#4CAF50` | 76, 175, 80 | 성공 메시지, 완료 표시 |
| **Warning** | `#FFC107` | 255, 193, 7 | 주의 메시지 |
| **Error** | `#F44336` | 244, 67, 54 | 오류, 삭제 버튼 |
| **Info** | `#03A9F4` | 3, 169, 244 | 정보 메시지 |

### 2.4 Neutral Colors (중성 색상)

| Name | Hex | RGB | Usage |
|------|-----|-----|-------|
| **Background** | `#FAFAFA` | 250, 250, 250 | App 전체 배경 |
| **Surface** | `#FFFFFF` | 255, 255, 255 | Card, Panel 배경 |
| **Border** | `#E0E0E0` | 224, 224, 224 | 구분선, 테두리 |
| **Divider** | `#EEEEEE` | 238, 238, 238 | 얇은 구분선 |
| **Text Primary** | `#212121` | 33, 33, 33 | 주요 텍스트 |
| **Text Secondary** | `#757575` | 117, 117, 117 | 보조 텍스트, Hint |
| **Text Disabled** | `#BDBDBD` | 189, 189, 189 | 비활성 텍스트 |

### 2.5 Layer Default Colors (레이어 기본 색상)

| Geometry | Fill | Stroke | Usage |
|----------|------|--------|-------|
| **Point** | `#E91E63` | `#C2185B` | Point Layer 기본색 |
| **LineString** | - | `#3F51B5` | Line Layer 기본색 |
| **Polygon** | `#009688` (50% opacity) | `#00796B` | Polygon Layer 기본색 |

### 2.6 Selection & Highlight (선택/하이라이트)

| State | Color | Usage |
|-------|-------|-------|
| **Selected Feature** | `#FFEB3B` (stroke) | 선택된 Feature 테두리 |
| **Hover Row** | `#E3F2FD` | Table Row Hover 배경 |
| **Selected Row** | `#BBDEFB` | Table Row 선택 배경 |
| **Focus Ring** | `#2196F3` (outline) | Focus 링 |

---

## 3. Typography (타이포그래피)

### 3.1 Font Family (글꼴)

| Purpose | Font | Fallback |
|---------|------|----------|
| **UI Text** | Segoe UI | -apple-system, sans-serif |
| **Monospace** | Consolas | Courier New, monospace |
| **Korean** | Malgun Gothic | NanumGothic, sans-serif |

### 3.2 Type Scale (글자 크기)

| Element | Size | Weight | Line Height | Usage |
|---------|------|--------|-------------|-------|
| **H1 / App Title** | 20px | SemiBold (600) | 28px | App 제목 |
| **H2 / Panel Header** | 16px | SemiBold (600) | 24px | Panel 제목 |
| **H3 / Section Title** | 14px | SemiBold (600) | 20px | Section 제목 |
| **Body** | 13px | Regular (400) | 20px | 본문 텍스트 |
| **Body Small** | 12px | Regular (400) | 18px | 보조 텍스트 |
| **Caption** | 11px | Regular (400) | 16px | Caption, Hint |
| **Button** | 13px | Medium (500) | 20px | Button Label |
| **Mono** | 12px | Regular (400) | 16px | 좌표, 수치 |

### 3.3 XAML Style Resources

```xml
<!-- Typography Styles -->
<Style x:Key="H1TextStyle" TargetType="TextBlock">
    <Setter Property="FontSize" Value="20"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="Foreground" Value="#212121"/>
</Style>

<Style x:Key="H2TextStyle" TargetType="TextBlock">
    <Setter Property="FontSize" Value="16"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="Foreground" Value="#212121"/>
</Style>

<Style x:Key="BodyTextStyle" TargetType="TextBlock">
    <Setter Property="FontSize" Value="13"/>
    <Setter Property="FontWeight" Value="Normal"/>
    <Setter Property="Foreground" Value="#212121"/>
</Style>

<Style x:Key="CaptionTextStyle" TargetType="TextBlock">
    <Setter Property="FontSize" Value="11"/>
    <Setter Property="Foreground" Value="#757575"/>
</Style>

<Style x:Key="MonoTextStyle" TargetType="TextBlock">
    <Setter Property="FontFamily" Value="Consolas"/>
    <Setter Property="FontSize" Value="12"/>
    <Setter Property="Foreground" Value="#424242"/>
</Style>
```

---

## 4. Spacing & Layout (간격 및 레이아웃)

### 4.1 Spacing Scale (간격 스케일)

| Token | Value | Usage |
|-------|-------|-------|
| **xs** | 4px | 아이콘-텍스트 간격 |
| **sm** | 8px | 요소 내부 여백 |
| **md** | 16px | 요소 간 간격 |
| **lg** | 24px | Section 간 간격 |
| **xl** | 32px | 큰 Section 구분 |

### 4.2 Main Layout Structure (메인 레이아웃)

```
┌────────────────────────────────────────────────────────────────────────┐
│  TOOLBAR                                                    Height: 48px│
│  ┌──────┐ ┌──────────────────────────────┐ ┌─────────────────────────┐ │
│  │ Logo │ │ Action Buttons               │ │ Settings            ⚙️ │ │
│  └──────┘ └──────────────────────────────┘ └─────────────────────────┘ │
├────────────────┬───────────────────────────────────────────────────────┤
│ LAYER PANEL    │ MAP VIEW                                              │
│ Width: 280px   │                                                       │
│ Min: 200px     │                                                       │
│ Max: 400px     │                                                       │
│                │                                                       │
│ ┌────────────┐ │                                                       │
│ │ Layer 1  ☑ │ │                                                       │
│ ├────────────┤ │              [Map Content Area]                       │
│ │ Layer 2  ☑ │ │                                                       │
│ ├────────────┤ │                                                       │
│ │ Layer 3  ☐ │ │                                                       │
│ └────────────┘ │                                                       │
│                │                                                       │
├────────────────┴───────────────────────────────────────────────────────┤
│ ATTRIBUTE PANEL (Collapsible)                           Height: 200px  │
│ ┌────────────────────────────────────────────────────────────────────┐ │
│ │ DataGrid - Feature Attributes                                      │ │
│ └────────────────────────────────────────────────────────────────────┘ │
├────────────────────────────────────────────────────────────────────────┤
│ STATUS BAR                                               Height: 24px  │
│ X: 127.0234  Y: 37.5123  │  Scale: 1:25000  │  EPSG:4326  │  Ready    │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 5. UI Components (UI 컴포넌트)

### 5.1 Toolbar (툴바)

| Property | Value |
|----------|-------|
| Height | 48px |
| Background | `#FFFFFF` |
| Shadow | `0 1px 3px rgba(0,0,0,0.12)` |
| Icon Size | 24px |
| Button Size | 40x40px |
| Button Padding | 8px |
| Separator | 1px `#E0E0E0`, Margin 8px |

### 5.2 Buttons (버튼)

| Type | Specs |
|------|-------|
| **Primary Button** | Background: `#2196F3`, Text: White, Radius: 4px, Padding: 8px 16px, Height: 36px |
| **Secondary Button** | Border: 1px `#2196F3`, Text: `#2196F3`, Background: Transparent |
| **Icon Button** | Size: 40x40px, Hover: `#E3F2FD`, Radius: 4px |
| **Danger Button** | Background: `#F44336`, Text: White |
| **Disabled** | Background: `#E0E0E0`, Text: `#9E9E9E` |

### 5.3 Layer Panel (레이어 패널)

| Property | Value |
|----------|-------|
| Default Width | 280px |
| Min Width | 200px |
| Max Width | 400px |
| Background | `#FFFFFF` |
| Header Height | 40px |
| Item Height | 44px |
| Item Padding | 8px 12px |
| Selected Background | `#E3F2FD` |
| Hover Background | `#F5F5F5` |
| Drag Handle | 6px dots |

### 5.4 Layer Item (레이어 항목)

```
Normal State:
┌─────────────────────────────────────────────────────┐
│ ⋮⋮ ☑ [🔷] 행정구역 레이어                      ⋮  │  44px
│    └ Drag  └ Checkbox └ Icon └ Name          └ Menu│
└─────────────────────────────────────────────────────┘

Expanded State:
┌─────────────────────────────────────────────────────┐
│ ⋮⋮ ☑ [🔷] 행정구역 레이어                      ⋮  │
│    Opacity: ═══════════○────── 70%                  │
└─────────────────────────────────────────────────────┘
```

### 5.5 Data Grid (데이터 그리드)

| Property | Value |
|----------|-------|
| Header Height | 32px |
| Header Background | `#F5F5F5` |
| Header Font Weight | SemiBold |
| Row Height | 28px |
| Alternate Row | `#FAFAFA` |
| Selected Row | `#E3F2FD` |
| Hover Row | `#F5F5F5` |
| Border | 1px `#E0E0E0` |
| Cell Padding | 8px |

### 5.6 Status Bar (상태 바)

| Property | Value |
|----------|-------|
| Height | 24px |
| Background | `#F5F5F5` |
| Text Size | 11px |
| Text Color | `#616161` |
| Padding | 0 12px |
| Separator | `│` (vertical bar) |

### 5.7 Dialog (다이얼로그)

| Property | Value |
|----------|-------|
| Min Width | 400px |
| Max Width | 600px |
| Border Radius | 8px |
| Shadow | `0 8px 24px rgba(0,0,0,0.15)` |
| Header Height | 56px |
| Header Padding | 16px 24px |
| Content Padding | 24px |
| Footer Height | 64px |
| Footer Padding | 16px 24px |
| Footer Background | `#FAFAFA` |

### 5.8 Input Fields (입력 필드)

| Property | Value |
|----------|-------|
| Height | 36px |
| Border | 1px `#E0E0E0` |
| Border Radius | 4px |
| Padding | 8px 12px |
| Focus Border | 2px `#2196F3` |
| Error Border | 2px `#F44336` |
| Placeholder Color | `#9E9E9E` |

### 5.9 Slider (슬라이더)

| Property | Value |
|----------|-------|
| Track Height | 4px |
| Track Color | `#E0E0E0` |
| Fill Color | `#2196F3` |
| Thumb Size | 16px |
| Thumb Color | `#2196F3` |
| Thumb Hover | `#1976D2` |

---

## 6. Icons (아이콘)

### 6.1 Icon Set

**Material Design Icons** 사용  
Website: https://materialdesignicons.com/

### 6.2 Icon Sizes

| Context | Size |
|---------|------|
| Toolbar | 24px |
| Menu Item | 20px |
| Button (with text) | 18px |
| Small/Inline | 16px |

### 6.3 Common Icons

| Action | Icon Name | Code |
|--------|-----------|------|
| Open File | `folder-open` | `\uF0770` |
| Save | `content-save` | `\uF0193` |
| Save As | `content-save-edit` | `\uF0CFB` |
| Add Layer | `layers-plus` | `\uF0E4C` |
| Remove Layer | `layers-remove` | `\uF0E4D` |
| Delete | `delete` | `\uF01B4` |
| Zoom In | `magnify-plus` | `\uF0349` |
| Zoom Out | `magnify-minus` | `\uF034A` |
| Zoom Extent | `fit-to-screen` | `\uF18F4` |
| Pan | `cursor-move` | `\uF01DB` |
| Select | `cursor-default-click` | `\uF0CFD` |
| Settings | `cog` | `\uF0493` |
| Table | `table` | `\uF04EB` |
| Visible | `eye` | `\uF0208` |
| Hidden | `eye-off` | `\uF0209` |
| Point | `circle` | `\uF0765` |
| Line | `vector-line` | `\uF0561` |
| Polygon | `vector-polygon` | `\uF0562` |
| Refresh | `refresh` | `\uF0450` |
| Undo | `undo` | `\uF054C` |
| Redo | `redo` | `\uF044E` |

---

## 7. Animation & Transitions (애니메이션)

### 7.1 Duration (지속 시간)

| Type | Duration | Easing |
|------|----------|--------|
| **Micro** | 100ms | ease-out |
| **Fast** | 200ms | ease-out |
| **Normal** | 300ms | ease-in-out |
| **Slow** | 500ms | ease-in-out |

### 7.2 Common Animations

| Element | Animation | Duration |
|---------|-----------|----------|
| Button Hover | Background color | 100ms |
| Panel Expand/Collapse | Height | 200ms |
| Dialog Open | Fade + Scale | 200ms |
| Toast Notification | Slide + Fade | 300ms |
| Loading Spinner | Rotation | Infinite |

### 7.3 XAML Animation Example

```xml
<!-- Button Hover Animation -->
<Style x:Key="AnimatedButtonStyle" TargetType="Button">
    <Style.Triggers>
        <Trigger Property="IsMouseOver" Value="True">
            <Trigger.EnterActions>
                <BeginStoryboard>
                    <Storyboard>
                        <ColorAnimation 
                            Storyboard.TargetProperty="(Button.Background).(SolidColorBrush.Color)"
                            To="#1976D2" 
                            Duration="0:0:0.1"/>
                    </Storyboard>
                </BeginStoryboard>
            </Trigger.EnterActions>
        </Trigger>
    </Style.Triggers>
</Style>
```

---

## 8. Responsive Behavior (반응형 동작)

### 8.1 Window Size Handling

| Window State | Behavior |
|--------------|----------|
| **< 1024px width** | Layer Panel auto-collapse |
| **< 768px width** | Attribute Panel hidden |
| **Maximized** | Full layout |
| **Restored** | Remember last size/position |

### 8.2 Panel Resize

| Panel | Behavior |
|-------|----------|
| **Layer Panel** | Drag to resize (200-400px) |
| **Attribute Panel** | Drag to resize (100-400px) |
| **Splitter** | 5px drag area |

