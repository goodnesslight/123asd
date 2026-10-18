using System;
using System.Collections;
using UnityEngine;

namespace BestStop
{
    public sealed class BestStopGame : MonoBehaviour
    {
        public const float Width = 2048, Height = 1152;
        // Calibrated against the entrance and phone booth in the background art.
        public const float CharacterHeight = 150f;
        public static readonly Rect WalkArea = Rect.MinMaxRect(38, 160, 2010, 298);
        Camera sceneCamera;
        Transform traveler;
        SpriteRenderer body;
        Sprite[] frames;
        Vector2 feet = new Vector2(1030, 225);
        readonly Transform[] clouds = new Transform[5];
        readonly float[] cloudX = { 180, 620, 1030, 1490, 1920 };
        readonly float[] cloudSpeed = { 9, 5, 12, 7, 10 };
        readonly float[] cloudY = { 1010, 950, 1050, 970, 1060 };
        float animationTime;
        float idleTime;
        Vector3 bodyRestScale;
        int direction;
        bool help, paused;
        GUIStyle label;

        void Awake()
        {
            Application.targetFrameRate = 60;
            // Override the windowed mode remembered by earlier builds.
            if (!Application.isEditor)
                Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.FullScreenWindow);
            sceneCamera = Camera.main;
            if (sceneCamera == null) sceneCamera = new GameObject("Orthographic camera").AddComponent<Camera>();
            sceneCamera.orthographic = true;
            sceneCamera.orthographicSize = Height / 2;
            sceneCamera.transform.position = new Vector3(Width / 2, Height / 2, -10);
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = new Color32(9, 22, 30, 255);
            // Environment and props are serialized scene objects, editable outside Play mode.
            CreateClouds();
            frames = SliceTraveler(Resources.Load<Texture2D>("Traveler"));
            var backTexture = Resources.Load<Texture2D>("TravelerBack");
            var backFrames = SliceTraveler(backTexture, 1);
            // Normalize the new sheet to the existing character's standing height.
            float backPixelsPerUnit = backFrames[0].rect.height / frames[0].rect.height;
            Array.Resize(ref frames, 12);
            for (int i = 0; i < backFrames.Length; i++)
            {
                frames[8 + i] = Sprite.Create(backTexture, backFrames[i].rect, new Vector2(0.5f, 0), backPixelsPerUnit);
                Destroy(backFrames[i]);
            }
            traveler = new GameObject("Traveler — WASD / arrows").transform;
            body = new GameObject("Animated sprite").AddComponent<SpriteRenderer>();
            body.transform.SetParent(traveler, false);
            body.sortingOrder = 2;
            body.sprite = frames[0];
            body.gameObject.AddComponent<GroundSort>().groundPoint = traveler;
            // Every pose uses one shared scale; feet pivots are aligned when slicing.
            float scale = CharacterHeight / frames[0].rect.height;
            body.transform.localScale = Vector3.one * scale;
            bodyRestScale = body.transform.localScale;
            CreateShadow();
            ApplyPosition();
            ResizeCamera();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--smoke-test") >= 0)
                StartCoroutine(SmokeTest());
        }

        static Sprite[] SliceTraveler(Texture2D texture, int rows = 2)
        {
            var result = new Sprite[4 * rows];
            int cw = texture.width / 4, ch = texture.height / rows;
            for (int row = 0; row < rows; row++)
            for (int col = 0; col < 4; col++)
            {
                int x0 = col * cw, y0 = (rows - 1 - row) * ch;
                int left = cw, right = 0, bottom = ch, top = 0;
                for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++)
                    if (texture.GetPixel(x0 + x, y0 + y).a > 0.25f)
                    {
                        left = Mathf.Min(left, x); right = Mathf.Max(right, x);
                        bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
                    }
                if (right < left) throw new InvalidOperationException("Empty traveler frame");
                result[row * 4 + col] = Sprite.Create(texture,
                    new Rect(x0 + left, y0 + bottom, right - left + 1, top - bottom + 1),
                    new Vector2(0.5f, 0), 1);
            }
            return result;
        }

        void CreateClouds()
        {
            Texture2D texture = Resources.Load<Texture2D>("Cloud");
            // Trim transparent padding when constructing the runtime sprite.
            int left = texture.width, bottom = texture.height, right = 0, top = 0;
            Color32[] pixels = texture.GetPixels32();
            for (int y = 0; y < texture.height; y++)
            for (int x = 0; x < texture.width; x++)
                if (pixels[y * texture.width + x].a > 64)
                {
                    left = Mathf.Min(left, x); right = Mathf.Max(right, x);
                    bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
                }
            Sprite sprite = Sprite.Create(texture, new Rect(left, bottom, right - left + 1, top - bottom + 1), new Vector2(0.5f, 0.5f), 1);
            float[] widths = { 220, 150, 250, 190, 180 };
            for (int i = 0; i < clouds.Length; i++)
            {
                var renderer = new GameObject("Drifting cloud " + (i + 1)).AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = -30;
                renderer.flipX = i % 2 == 1;
                renderer.transform.localScale = Vector3.one * (widths[i] / sprite.rect.width);
                clouds[i] = renderer.transform;
            }
            AdvanceClouds(0);
        }

        void AdvanceClouds(float dt)
        {
            // Wrap only after the entire cloud leaves the picture, outside the viewport.
            for (int i = 0; i < clouds.Length; i++)
            {
                cloudX[i] = Mathf.Repeat(cloudX[i] + 220 + cloudSpeed[i] * dt, Width + 440) - 220;
                clouds[i].position = new Vector3(cloudX[i], cloudY[i], 0);
            }
        }

        void CreateShadow()
        {
            var texture = new Texture2D(48, 12, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            for (int y = 0; y < 12; y++)
            for (int x = 0; x < 48; x++)
            {
                float d = Mathf.Pow((x - 23.5f) / 24, 2) + Mathf.Pow((y - 5.5f) / 6, 2);
                texture.SetPixel(x, y, d < 1 ? new Color(0.02f, 0.05f, 0.09f, 0.30f) : Color.clear);
            }
            texture.Apply();
            var shadow = new GameObject("Contact shadow").AddComponent<SpriteRenderer>();
            shadow.transform.SetParent(traveler, false);
            shadow.transform.localScale = new Vector3(CharacterHeight / 102f, 1.25f, 1);
            shadow.sprite = Sprite.Create(texture, new Rect(0, 0, 48, 12), new Vector2(0.5f, 0.5f), 1);
            shadow.sortingOrder = 1;
            var shadowSort = shadow.gameObject.AddComponent<GroundSort>();
            shadowSort.groundPoint = traveler;
            shadowSort.orderOffset = -1;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) help = !help;
            if (Input.GetKeyDown(KeyCode.F11))
            {
                if (Screen.fullScreen)
                    Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
                else
                    Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.FullScreenWindow);
            }
            if (Input.GetKeyDown(KeyCode.Escape)) paused = !paused;
            if (Input.GetKeyDown(KeyCode.R)) { feet = new Vector2(1030, 225); ApplyPosition(); }
            if (paused) return;
            AdvanceClouds(Time.deltaTime);
            Vector2 input = Vector2.zero;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) input.x--;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input.x++;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) input.y--;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) input.y++;
            bool running = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            Move(input, running, Time.deltaTime);
        }

        public static Vector2 Constrain(Vector2 position) => new Vector2(
            Mathf.Clamp(position.x, WalkArea.xMin, WalkArea.xMax),
            Mathf.Clamp(position.y, WalkArea.yMin, WalkArea.yMax));

        void Move(Vector2 input, bool running, float dt)
        {
            bool moving = input.sqrMagnitude > 0;
            feet = Constrain(feet + Vector2.ClampMagnitude(input, 1) * (running ? 280 : 155) * dt);
            if (moving)
            {
                // Vertical input takes priority, including diagonals toward the store.
                direction = input.y > 0 ? 2 : input.y < 0 ? 0 : 1;
                body.flipX = direction == 1 && input.x < 0;
                animationTime += dt * (running ? 10 : 6);
                idleTime = 0;
                body.transform.localScale = bodyRestScale;
                body.transform.localRotation = Quaternion.identity;
            }
            else
            {
                animationTime = 0;
                AnimateIdle(dt);
            }
            body.sprite = frames[direction * 4 + (moving ? (int)animationTime % 4 : 0)];
            ApplyPosition();
        }

        void AnimateIdle(float dt)
        {
            // Animate the existing art without changing the character design.
            // Bottom-center sprite pivots keep both feet anchored to the ground.
            idleTime = Mathf.Repeat(idleTime + dt, 12f);
            float breath = (1f - Mathf.Cos(idleTime * Mathf.PI * 2f / 3f)) * 0.5f;
            float settle = Mathf.SmoothStep(0, 1, Mathf.Min(idleTime / 0.4f, 1));
            body.transform.localScale = new Vector3(
                bodyRestScale.x * (1f - breath * 0.006f),
                bodyRestScale.y * (1f + breath * 0.018f), bodyRestScale.z);
            // Small weight shift; maximum tilt is less than half a degree.
            float sway = Mathf.Sin(idleTime * Mathf.PI * 2f / 4f) * 0.35f * settle;
            body.transform.localRotation = Quaternion.Euler(0, 0, sway);
        }

        void ApplyPosition() => traveler.position = new Vector3(Mathf.Round(feet.x), Mathf.Round(feet.y), 0);

        void LateUpdate() => ResizeCamera();

        void ResizeCamera()
        {
            FitCamera((float)Screen.width / Mathf.Max(1, Screen.height));
        }

        void FitCamera(float aspect)
        {
            // Cover the entire viewport, keeping the art's proportions.
            // Track the player and clamp the view inside the original image.
            float halfHeight = Mathf.Min(Height / 2, Width / (2 * aspect));
            float halfWidth = halfHeight * aspect;
            sceneCamera.rect = new Rect(0, 0, 1, 1);
            sceneCamera.aspect = aspect;
            sceneCamera.orthographicSize = halfHeight;
            sceneCamera.transform.position = new Vector3(
                Mathf.Clamp(feet.x, halfWidth, Width - halfWidth),
                Mathf.Clamp(feet.y + 150, halfHeight, Height - halfHeight), -10);
        }

        void OnGUI()
        {
            if (label == null) label = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            if (!help && !paused) return;
            GUI.Box(new Rect(Screen.width / 2 - 280, 18, 560, paused ? 94 : 66), GUIContent.none);
            GUI.Label(new Rect(Screen.width / 2 - 270, 25, 540, 52),
                "WASD / Arrows — move    Shift — run    R — reset\nF1 — controls    F11 — fullscreen    Esc — pause", label);
            if (paused) GUI.Label(new Rect(Screen.width / 2 - 270, 77, 540, 26), "PAUSED — Esc to resume", label);
        }

        IEnumerator SmokeTest()
        {
            yield return null;
            Vector2 start = feet;
            Move(Vector2.right, false, 1);
            if (Mathf.Abs(feet.x - start.x - 155) > 0.01f) throw new Exception("Walking speed test failed");
            Move(Vector2.one, true, 100);
            if (feet != new Vector2(WalkArea.xMax, WalkArea.yMax)) throw new Exception("Upper bounds test failed");
            Move(-Vector2.one, true, 100);
            if (feet != new Vector2(WalkArea.xMin, WalkArea.yMin)) throw new Exception("Lower bounds test failed");
            feet = start;
            Move(Vector2.zero, false, 0);
            if (frames.Length != 12 || body.sprite == null) throw new Exception("Animation test failed");
            Move(Vector2.up, false, 0);
            if (direction != 2 || body.sprite != frames[8] || body.flipX)
                throw new Exception("Back-facing movement test failed");
            Move(Vector2.zero, false, 0);
            if (direction != 2 || body.sprite != frames[8])
                throw new Exception("Back-facing idle test failed");
            Move(new Vector2(-1, 1), false, 0);
            if (direction != 2 || body.flipX) throw new Exception("Back-facing diagonal test failed");
            Move(Vector2.down, false, 0);
            if (direction != 0 || body.sprite != frames[0]) throw new Exception("Front-facing test failed");
            Move(Vector2.left, false, 0);
            if (direction != 1 || !body.flipX) throw new Exception("Left-facing test failed");
            Move(Vector2.up, false, 0);
            if (Mathf.Abs(frames[8].bounds.size.y - frames[0].bounds.size.y) > 0.01f)
                throw new Exception("Back-facing sprite scale test failed");
            Vector3 idleFeet = traveler.position;
            bool idleFacing = body.flipX;
            Move(Vector2.zero, false, 1.5f);
            if (body.transform.localScale.y <= bodyRestScale.y || traveler.position != idleFeet || body.flipX != idleFacing)
                throw new Exception("Idle breathing / stationary feet / facing test failed");
            Move(Vector2.zero, false, 1.5f);
            if (Vector3.Distance(body.transform.localScale, bodyRestScale) > 0.0001f)
                throw new Exception("Idle breathing loop test failed");
            Move(Vector2.right, false, 0);
            if (body.transform.localScale != bodyRestScale || body.transform.localRotation != Quaternion.identity || idleTime != 0)
                throw new Exception("Idle to walk transition test failed");
            Move(Vector2.zero, false, 0);
            foreach (float aspect in new[] { 16f / 9, 16f / 10, 4f / 3, 21f / 9, 32f / 9 })
            foreach (float x in new[] { WalkArea.xMin, start.x, WalkArea.xMax })
            {
                feet.x = x;
                FitCamera(aspect);
                float hh = sceneCamera.orthographicSize, hw = hh * aspect;
                Vector3 p = sceneCamera.transform.position;
                if (sceneCamera.rect != new Rect(0, 0, 1, 1) ||
                    p.x - hw < -0.01f || p.x + hw > Width + 0.01f ||
                    p.y - hh < -0.01f || p.y + hh > Height + 0.01f ||
                    x < p.x - hw || x > p.x + hw)
                    throw new Exception("Fullscreen camera coverage test failed");
            }
            feet = start;
            Move(Vector2.up, false, 0);
            Move(Vector2.zero, false, 0);
            ResizeCamera();
            float initialCloudX = cloudX[0];
            AdvanceClouds(2);
            if (Mathf.Abs(cloudX[0] - initialCloudX - cloudSpeed[0] * 2) > 0.01f)
                throw new Exception("Cloud movement test failed");
            AdvanceClouds(10000);
            for (int i = 0; i < clouds.Length; i++)
                if (cloudX[i] < -220 || cloudX[i] >= Width + 220)
                    throw new Exception("Cloud wrap test failed");
            float[] resetClouds = { 180, 620, 1030, 1490, 1920 };
            Array.Copy(resetClouds, cloudX, clouds.Length);
            AdvanceClouds(0);
            FitCamera(16f / 9);
            if (Mathf.Abs(sceneCamera.orthographicSize - Height / 2) > 0.01f ||
                Mathf.Abs(sceneCamera.transform.position.x - Width / 2) > 0.01f)
                throw new Exception("Full 16:9 composition test failed");
            yield return new WaitForEndOfFrame();
            int captureWidth = Screen.width, captureHeight = Screen.height;
            var target = new RenderTexture(captureWidth, captureHeight, 24);
            sceneCamera.targetTexture = target;
            FitCamera((float)captureWidth / captureHeight);
            sceneCamera.Render();
            RenderTexture.active = target;
            var screenshot = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
            screenshot.Apply();
            string path = System.IO.Path.Combine(Application.dataPath, "..", "preview.png");
            System.IO.File.WriteAllBytes(path, screenshot.EncodeToPNG());
            RenderTexture.active = null;
            sceneCamera.targetTexture = null;
            target.Release();
            Debug.Log("BEST_STOP_SMOKE_PASS: movement, boundaries, back/front/side facing, back idle and diagonal, sprite scale, idle breathing loop and walk transition, full 16:9 composition, cloud movement and wrapping, camera coverage at five aspect ratios; fullscreen=" + Screen.fullScreen + "; screenshot " + captureWidth + "x" + captureHeight + " " + path);
            Application.Quit(0);
        }
    }
}
