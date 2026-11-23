using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CatFishGame
{
    /// <summary>
    /// Главная форма игры. Реализует игровой цикл, физику котика,
    /// коллизии с платформами, сбор рыбок, шипы, врагов, таймер и
    /// переход между уровнями. После третьего уровня (или проигрыша)
    /// открывает форму итогов.
    /// </summary>
    public class GameForm : Form
    {
        // ---------- Игровое поле ----------
        private const int FieldWidth = LevelFactory.WorldWidth;
        private const int FieldHeight = LevelFactory.WorldHeight;
        private const int HudHeight = 60; // верхняя полоса HUD

        // ---------- Таймеры ----------
        private Timer frameTimer;     // цикл отрисовки/физики (~60 fps)
        private Timer secondTimer;    // ежесекундный таймер (для HUD)

        // ---------- Состояние ----------
        private List<Level> levels;
        private int levelIndex = 0;
        private Level CurrentLevel { get { return levels[levelIndex]; } }

        // Игрок
        private float px, py;         // позиция котика
        private float vx, vy;         // скорость
        private bool onGround = false;
        private bool facingRight = true;
        private const float Gravity = 0.6f;
        private const float MoveSpeed = 4.2f;
        private const float JumpVel = -12.5f;
        private const float MaxFall = 16f;
        private const int PlayerW = 36;
        private const int PlayerH = 40;

        // Состояние ввода
        private bool keyLeft, keyRight, keyJump;

        // Состояние игры
        private int tick = 0;
        private int timeRemaining;    // в секундах
        private int totalFishAll = 0; // всего собрано рыбок за все уровни
        private int totalTimeUsed = 0;// суммарно потрачено секунд
        private bool paused = false;
        private string flashMessage = "";   // краткое сообщение поверх экрана
        private int flashTimer = 0;          // сколько кадров показывать

        // Фаза перехода
        private enum Phase { Playing, LevelDone, GameOver, AllDone }
        private Phase phase = Phase.Playing;
        private int phaseTimer = 0;

        public GameForm()
        {
            this.Text = "Котик и Рыбки";
            this.ClientSize = new Size(FieldWidth, FieldHeight + HudHeight);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.BackColor = Color.FromArgb(120, 200, 240);
            this.DoubleBuffered = true;
            this.KeyPreview = true;

            levels = LevelFactory.CreateAll();
            LoadLevel(0);

            // Игровой таймер (~60 fps)
            frameTimer = new Timer();
            frameTimer.Interval = 16;
            frameTimer.Tick += FrameTimer_Tick;
            frameTimer.Start();

            // Секундный таймер — тикает только в фазе Playing
            secondTimer = new Timer();
            secondTimer.Interval = 1000;
            secondTimer.Tick += SecondTimer_Tick;
            secondTimer.Start();
        }

        // ---------- Загрузка уровня ----------
        private void LoadLevel(int idx)
        {
            levelIndex = idx;
            var lvl = CurrentLevel;
            px = lvl.StartPosition.X;
            py = lvl.StartPosition.Y;
            vx = vy = 0;
            onGround = false;
            facingRight = true;
            timeRemaining = lvl.TimeLimit;
            phase = Phase.Playing;
            phaseTimer = 0;
            ShowFlash("Уровень " + lvl.LevelNumber + ": " + lvl.Name, 90);
            this.Invalidate();
        }

        // ---------- Ввод ----------
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Left:  keyLeft = true; facingRight = false; break;
                case Keys.Right: keyRight = true; facingRight = true; break;
                case Keys.Up:
                case Keys.Space: keyJump = true; break;
                case Keys.P: paused = !paused; break;
                case Keys.Escape:
                    if (phase != Phase.Playing || paused)
                    {
                        ReturnToMenu();
                    }
                    else
                    {
                        paused = true;
                    }
                    break;
            }
            e.Handled = true;
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            switch (e.KeyCode)
            {
                case Keys.Left:  keyLeft = false; break;
                case Keys.Right: keyRight = false; break;
                case Keys.Up:
                case Keys.Space: keyJump = false; break;
            }
            e.Handled = true;
        }

        // ---------- Игровой цикл ----------
        private void FrameTimer_Tick(object sender, EventArgs e)
        {
            tick++;

            if (flashTimer > 0) flashTimer--;

            if (paused) { this.Invalidate(); return; }

            if (phase == Phase.Playing)
            {
                UpdatePhysics();
                UpdateLevelEntities();
                CheckHazards();
                CheckFishCollection();
                CheckExitReached();
            }
            else if (phase == Phase.LevelDone)
            {
                phaseTimer--;
                if (phaseTimer <= 0)
                {
                    if (levelIndex + 1 < levels.Count)
                    {
                        LoadLevel(levelIndex + 1);
                    }
                    else
                    {
                        phase = Phase.AllDone;
                        phaseTimer = 60;
                    }
                }
            }
            else if (phase == Phase.GameOver)
            {
                phaseTimer--;
                if (phaseTimer <= 0)
                {
                    OpenResults(victory: false);
                    return;
                }
            }
            else if (phase == Phase.AllDone)
            {
                phaseTimer--;
                if (phaseTimer <= 0)
                {
                    OpenResults(victory: true);
                    return;
                }
            }

            this.Invalidate();
        }

        private void SecondTimer_Tick(object sender, EventArgs e)
        {
            if (paused) return;
            if (phase != Phase.Playing) return;
            timeRemaining--;
            totalTimeUsed++;
            if (timeRemaining <= 0)
            {
                timeRemaining = 0;
                phase = Phase.GameOver;
                phaseTimer = 90;
                ShowFlash("Время вышло!", 90);
            }
        }

        // ---------- Физика и движение ----------

        private void UpdatePhysics()
        {
            // 1) Запоминаем платформу, на которой котик стоит прямо сейчас,
            //    чтобы потом «перевезти» его при движении этой платформы.
            MovingPlatform standingOn = null;
            if (onGround)
            {
                var pr0 = PlayerRect;
                Rectangle feet = new Rectangle(pr0.X + 2, pr0.Bottom, pr0.Width - 4, 3);
                foreach (var mp in CurrentLevel.MovingPlatforms)
                {
                    if (feet.IntersectsWith(mp.Bounds)) { standingOn = mp; break; }
                }
            }

            // 2) Обновляем все движущиеся платформы и применяем смещение
            //    к котику, если он стоит на такой платформе.
            foreach (var mp in CurrentLevel.MovingPlatforms)
            {
                int oldX = mp.Bounds.X;
                int oldY = mp.Bounds.Y;
                mp.Update();
                if (mp == standingOn)
                {
                    px += mp.Bounds.X - oldX;
                    py += mp.Bounds.Y - oldY;
                }
            }

            // 3) Горизонтальный ввод
            if (keyLeft && !keyRight)
            {
                vx = -MoveSpeed;
                facingRight = false;
            }
            else if (keyRight && !keyLeft)
            {
                vx = MoveSpeed;
                facingRight = true;
            }
            else
            {
                vx = 0;
            }

            // 4) Прыжок
            if (keyJump && onGround)
            {
                vy = JumpVel;
                onGround = false;
            }

            // 5) Гравитация
            vy += Gravity;
            if (vy > MaxFall) vy = MaxFall;

            // 6) Горизонтальное движение + коллизии
            px += vx;
            ResolveHorizontal();

            // 7) Вертикальное движение + коллизии
            py += vy;
            onGround = false;
            ResolveVertical();

            // 8) Не выпускаем за боковые границы мира
            if (px < 0) px = 0;
            if (px + PlayerW > FieldWidth) px = FieldWidth - PlayerW;

            // 9) Падение за нижнюю границу — возврат на старт
            if (py > FieldHeight + 60)
            {
                Respawn();
            }
        }

        private Rectangle PlayerRect
        {
            get { return new Rectangle((int)px, (int)py, PlayerW, PlayerH); }
        }

        private IEnumerable<Rectangle> AllSolidRects()
        {
            foreach (var p in CurrentLevel.Platforms) yield return p.Bounds;
            foreach (var mp in CurrentLevel.MovingPlatforms) yield return mp.Bounds;
        }

        private void ResolveHorizontal()
        {
            var pr = PlayerRect;
            foreach (var r in AllSolidRects())
            {
                if (pr.IntersectsWith(r))
                {
                    if (vx > 0) { px = r.Left - PlayerW; }
                    else if (vx < 0) { px = r.Right; }
                    vx = 0;
                    pr = PlayerRect;
                }
            }
        }

        private void ResolveVertical()
        {
            var pr = PlayerRect;
            foreach (var r in AllSolidRects())
            {
                if (pr.IntersectsWith(r))
                {
                    if (vy > 0)
                    {
                        py = r.Top - PlayerH;
                        vy = 0;
                        onGround = true;
                    }
                    else if (vy < 0)
                    {
                        py = r.Bottom;
                        vy = 0;
                    }
                    pr = PlayerRect;
                }
            }
        }

        // ---------- Сущности уровня ----------

        private void UpdateLevelEntities()
        {
            // Движущиеся платформы уже обновлены в UpdatePhysics —
            // здесь только двигаем врагов.
            foreach (var en in CurrentLevel.Enemies)
            {
                en.Update();
            }
        }

        // ---------- Опасности ----------

        private void CheckHazards()
        {
            var pr = PlayerRect;
            foreach (var sp in CurrentLevel.Spikes)
            {
                if (pr.IntersectsWith(sp.Bounds))
                {
                    Respawn();
                    return;
                }
            }
            foreach (var en in CurrentLevel.Enemies)
            {
                if (pr.IntersectsWith(en.Bounds))
                {
                    Respawn();
                    return;
                }
            }
        }

        private void Respawn()
        {
            px = CurrentLevel.StartPosition.X;
            py = CurrentLevel.StartPosition.Y;
            vx = vy = 0;
            onGround = false;
            ShowFlash("Ой! Возврат на старт", 60);
        }

        // ---------- Сбор рыбок ----------

        private void CheckFishCollection()
        {
            var pr = PlayerRect;
            foreach (var f in CurrentLevel.Fishes)
            {
                if (!f.Collected && pr.IntersectsWith(f.HitBox(tick)))
                {
                    f.Collected = true;
                    totalFishAll++;
                }
            }
        }

        // ---------- Выход ----------

        private void CheckExitReached()
        {
            var lvl = CurrentLevel;
            if (lvl.CollectedFishCount() < lvl.TotalFish) return;

            var pr = PlayerRect;
            if (pr.IntersectsWith(lvl.ExitDoor))
            {
                phase = Phase.LevelDone;
                phaseTimer = 90;
                ShowFlash("Уровень пройден!", 90);
            }
        }

        // ---------- Переход к результатам ----------

        private void OpenResults(bool victory)
        {
            frameTimer.Stop();
            secondTimer.Stop();
            var res = new ResultsForm(
                victory,
                totalFishAll,
                totalTimeUsed,
                levelIndex + 1,
                levels.Count);
            res.FormClosed += (s, e) => { ReturnToMenu(); };
            res.Show();
            this.Hide();
        }

        private void ReturnToMenu()
        {
            frameTimer.Stop();
            secondTimer.Stop();
            this.Close();
        }

        // ---------- Вспомогательное ----------

        private void ShowFlash(string msg, int frames)
        {
            flashMessage = msg;
            flashTimer = frames;
        }

        // ---------- Отрисовка ----------

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            DrawBackground(g);
            DrawLevel(g);
            DrawHUD(g);

            if (!string.IsNullOrEmpty(flashMessage) && flashTimer > 0)
            {
                DrawFlash(g);
            }

            if (paused)
            {
                DrawPauseOverlay(g);
            }
        }

        private void DrawBackground(Graphics g)
        {
            // Фон зависит от номера уровня
            Color top, bottom;
            switch (CurrentLevel.LevelNumber)
            {
                case 1: // Дворик
                    top = Color.FromArgb(135, 206, 235);
                    bottom = Color.FromArgb(190, 230, 200);
                    break;
                case 2: // Ручей
                    top = Color.FromArgb(100, 180, 220);
                    bottom = Color.FromArgb(70, 130, 180);
                    break;
                default: // Подземелье
                    top = Color.FromArgb(50, 50, 70);
                    bottom = Color.FromArgb(20, 20, 35);
                    break;
            }

            using (var bg = new LinearGradientBrush(
                new Rectangle(0, HudHeight, FieldWidth, FieldHeight),
                top, bottom, LinearGradientMode.Vertical))
            {
                g.FillRectangle(bg, 0, HudHeight, FieldWidth, FieldHeight);
            }
        }

        private void DrawLevel(Graphics g)
        {
            int yOff = HudHeight;

            // Смещаем всю отрисовку уровня вниз на высоту HUD
            g.TranslateTransform(0, yOff);

            // Платформы
            foreach (var p in CurrentLevel.Platforms)
            {
                DrawPlatform(g, p.Bounds, CurrentLevel.LevelNumber);
            }

            // Движущиеся платформы
            foreach (var mp in CurrentLevel.MovingPlatforms)
            {
                DrawPlatform(g, mp.Bounds, CurrentLevel.LevelNumber, isMoving: true);
            }

            // Шипы
            foreach (var sp in CurrentLevel.Spikes)
            {
                DrawSpikes(g, sp.Bounds);
            }

            // Враги
            foreach (var en in CurrentLevel.Enemies)
            {
                DrawEnemy(g, en);
            }

            // Рыбки
            foreach (var f in CurrentLevel.Fishes)
            {
                if (!f.Collected)
                {
                    int yo = f.BobOffset(tick);
                    DrawFish(g, f.Position.X, f.Position.Y + yo, 26, true);
                }
            }

            // Выход — дверь
            DrawExit(g, CurrentLevel.ExitDoor, CurrentLevel.CollectedFishCount() >= CurrentLevel.TotalFish);

            // Котик
            Color catColor = Color.FromArgb(255, 170, 80);
            MenuForm.DrawCat(g, (int)px, (int)py, PlayerH, catColor, facingRight);

            // Сбрасываем transform
            g.ResetTransform();
        }

        private void DrawPlatform(Graphics g, Rectangle r, int levelNum, bool isMoving = false)
        {
            Color top, side;
            if (levelNum == 1)
            {
                top = isMoving ? Color.FromArgb(180, 130, 80) : Color.FromArgb(140, 90, 50);
                side = Color.FromArgb(110, 70, 35);
            }
            else if (levelNum == 2)
            {
                top = Color.FromArgb(160, 110, 70);
                side = Color.FromArgb(110, 75, 45);
            }
            else
            {
                top = Color.FromArgb(90, 90, 100);
                side = Color.FromArgb(55, 55, 70);
            }
            using (var b = new SolidBrush(top)) g.FillRectangle(b, r);
            // Тень снизу
            using (var b = new SolidBrush(side)) g.FillRectangle(b, r.X, r.Bottom - 4, r.Width, 4);
            // Текстура: мелкие штрихи сверху
            using (var p = new Pen(Color.FromArgb(60, 0, 0, 0), 1))
            {
                for (int i = 4; i < r.Width; i += 12)
                    g.DrawLine(p, r.X + i, r.Y + 2, r.X + i, r.Y + 5);
            }
            // Для движущихся платформ — лёгкий жёлтый контур
            if (isMoving)
            {
                using (var p = new Pen(Color.FromArgb(255, 230, 120), 2))
                    g.DrawRectangle(p, r);
            }
        }

        private void DrawSpikes(Graphics g, Rectangle r)
        {
            // Серое основание
            using (var b = new SolidBrush(Color.FromArgb(70, 70, 80)))
                g.FillRectangle(b, r);

            // Шипы — треугольники
            int count = Math.Max(1, r.Width / 10);
            int stepW = r.Width / count;
            Color spikeColor = Color.FromArgb(200, 200, 210);
            Color spikeDark = Color.FromArgb(140, 140, 150);
            for (int i = 0; i < count; i++)
            {
                int bx = r.X + i * stepW;
                Point[] tri = {
                    new Point(bx, r.Bottom),
                    new Point(bx + stepW/2, r.Top),
                    new Point(bx + stepW, r.Bottom)
                };
                g.FillPolygon(new SolidBrush(spikeColor), tri);
                // Тёмная половинка
                Point[] tri2 = {
                    new Point(bx + stepW/2, r.Top),
                    new Point(bx + stepW, r.Bottom),
                    new Point(bx + stepW/2, r.Bottom)
                };
                g.FillPolygon(new SolidBrush(spikeDark), tri2);
            }
        }

        private void DrawEnemy(Graphics g, Enemy en)
        {
            var r = en.Bounds;
            // Тело собаки — коричневый прямоугольник
            Color body = Color.FromArgb(120, 75, 45);
            Color dark = Color.FromArgb(80, 50, 25);
            using (var b = new SolidBrush(body))
                g.FillRectangle(b, r.X, r.Y + r.Height/4, r.Width, r.Height*3/4);
            // Голова
            int headW = r.Width * 2 / 3;
            int headH = r.Height / 2;
            int hx = en.Direction > 0 ? r.Right - headW : r.X;
            using (var b = new SolidBrush(body))
                g.FillRectangle(b, hx, r.Y, headW, headH);
            // Уши
            Point[] ear1 = { new Point(hx, r.Y), new Point(hx, r.Y - 6), new Point(hx + 6, r.Y) };
            Point[] ear2 = { new Point(hx + headW - 6, r.Y), new Point(hx + headW, r.Y - 6), new Point(hx + headW, r.Y) };
            g.FillPolygon(new SolidBrush(dark), ear1);
            g.FillPolygon(new SolidBrush(dark), ear2);
            // Глаз
            int eyeX = en.Direction > 0 ? hx + headW - 12 : hx + 4;
            g.FillEllipse(Brushes.White, eyeX, r.Y + 4, 5, 5);
            g.FillEllipse(Brushes.Black, eyeX + 2, r.Y + 5, 3, 3);
            // Ноги (анимированные)
            bool step = (tick / 8) % 2 == 0;
            int legY = r.Bottom - 6;
            using (var b = new SolidBrush(dark))
            {
                g.FillRectangle(b, r.X + 4, legY + (step ? 0 : 2), 5, 6);
                g.FillRectangle(b, r.Right - 9, legY + (step ? 2 : 0), 5, 6);
            }
        }

        public static void DrawFish(Graphics g, int x, int y, int size, bool facingRight)
        {
            MenuForm.DrawFish(g, x, y, size, facingRight);
        }

        private void DrawExit(Graphics g, Rectangle r, bool ready)
        {
            // Дверная рама
            Color frame = Color.FromArgb(80, 50, 30);
            Color door = ready ? Color.FromArgb(220, 180, 90) : Color.FromArgb(140, 100, 60);
            using (var b = new SolidBrush(frame))
                g.FillRectangle(b, r.X - 4, r.Y - 4, r.Width + 8, r.Height + 4);
            using (var b = new SolidBrush(door))
                g.FillRectangle(b, r);

            // Ручка
            using (var b = new SolidBrush(Color.FromArgb(60, 40, 20)))
                g.FillEllipse(b, r.Right - 12, r.Y + r.Height/2 - 3, 6, 6);

            // Надпись над дверью
            string txt = ready ? "Выход открыт!" : "Собери рыбки";
            using (var f = new Font("Segoe UI", 9F, FontStyle.Bold))
            using (var b = new SolidBrush(ready ? Color.FromArgb(60, 120, 60) : Color.FromArgb(120, 60, 60)))
            {
                var sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                g.DrawString(txt, f, b, new RectangleF(r.X - 20, r.Y - 20, r.Width + 40, 18), sf);
            }
        }

        private void DrawHUD(Graphics g)
        {
            // Полоса HUD
            using (var b = new SolidBrush(Color.FromArgb(245, 240, 230)))
                g.FillRectangle(b, 0, 0, FieldWidth, HudHeight);
            using (var p = new Pen(Color.FromArgb(180, 160, 130), 1))
                g.DrawLine(p, 0, HudHeight, FieldWidth, HudHeight);

            using (var font = new Font("Segoe UI", 12F, FontStyle.Bold))
            {
                var lvl = CurrentLevel;
                string left = "Уровень " + lvl.LevelNumber + "/3  —  " + lvl.Name;
                string mid = "Рыбки: " + lvl.CollectedFishCount() + " / " + lvl.TotalFish;
                string right = "Время: " + timeRemaining + " с";

                using (var b = new SolidBrush(Color.FromArgb(70, 50, 30)))
                {
                    g.DrawString(left, font, b, 12, 18);
                    g.DrawString(mid, font, b, 320, 18);
                }
                // Время — цвет зависит от остатка
                Color timeColor = timeRemaining <= 10 ? Color.FromArgb(200, 50, 50) : Color.FromArgb(70, 50, 30);
                using (var b = new SolidBrush(timeColor))
                    g.DrawString(right, font, b, 620, 18);
            }
        }

        private void DrawFlash(Graphics g)
        {
            // Полупрозрачная подложка для текста
            using (var f = new Font("Segoe UI", 22F, FontStyle.Bold))
            {
                string msg = flashMessage;
                var size = g.MeasureString(msg, f);
                int bx = (FieldWidth - (int)size.Width) / 2 - 20;
                int by = (FieldHeight + HudHeight) / 2 - (int)size.Height / 2 - 10;
                int bw = (int)size.Width + 40;
                int bh = (int)size.Height + 20;

                using (var b = new SolidBrush(Color.FromArgb(200, 30, 30, 40)))
                    g.FillRoundedRectangle(b, bx, by, bw, bh, 12);
                using (var p = new Pen(Color.FromArgb(255, 230, 180), 2))
                    g.DrawRoundedRectangle(p, bx, by, bw, bh, 12);

                var sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                using (var b = new SolidBrush(Color.White))
                    g.DrawString(msg, f, b, new RectangleF(bx, by, bw, bh), sf);
            }
        }

        private void DrawPauseOverlay(Graphics g)
        {
            using (var b = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
                g.FillRectangle(b, 0, 0, FieldWidth, FieldHeight + HudHeight);
            using (var f = new Font("Segoe UI", 28F, FontStyle.Bold))
            using (var b = new SolidBrush(Color.White))
            {
                var sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                g.DrawString("Пауза", f, b,
                    new RectangleF(0, (FieldHeight + HudHeight) / 2 - 60, FieldWidth, 60), sf);
            }
            using (var f = new Font("Segoe UI", 12F))
            using (var b = new SolidBrush(Color.White))
            {
                var sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                g.DrawString("Нажмите P для продолжения, Esc — выход в меню", f, b,
                    new RectangleF(0, (FieldHeight + HudHeight) / 2 + 10, FieldWidth, 30), sf);
            }
        }
    }

    /// <summary>
    /// Расширения для рисования скруглённых прямоугольников (используются в HUD/флэш-сообщениях).
    /// </summary>
    internal static class GraphicsExtensions
    {
        public static void FillRoundedRectangle(this Graphics g, Brush b, int x, int y, int w, int h, int r)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = r * 2;
            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + w - d, y, d, d, 270, 90);
            path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            path.AddArc(x, y + h - d, d, d, 90, 90);
            path.CloseFigure();
            g.FillPath(b, path);
        }

        public static void DrawRoundedRectangle(this Graphics g, Pen p, int x, int y, int w, int h, int r)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = r * 2;
            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + w - d, y, d, d, 270, 90);
            path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            path.AddArc(x, y + h - d, d, d, 90, 90);
            path.CloseFigure();
            g.DrawPath(p, path);
        }
    }
}
