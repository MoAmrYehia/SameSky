// What the film's screens show: Maya's sunset clip and her two photos, filmed on the film's own waterfront set (so
// nothing on a screen in the trailer comes from anywhere else). Rendered first, as numbered pictures in
// demo-build/film/media, and read back by the shots that show a phone or the lounge's screen.

using UnityEngine;

namespace SameSky.Film
{
    public static partial class FilmShots
    {
        /// <summary>The sunset as Maya's phone films it from the bench: eight seconds, hand-held.</summary>
        static void MediaSunset(FilmStage s)
        {
            var set = FilmSets.Waterfront(s);
            var sun = new Vector3(-0.3f, 1.3f, 0.6f) + set.SunDirection * 60f;
            s.Camera.Position.Key(0f, -0.3f, 1.3f, 0.6f).Key(8f, -0.26f, 1.32f, 0.62f);
            s.Camera.Target.Key(0f, sun + new Vector3(-9f, -3.4f, 0f)).Key(8f, sun + new Vector3(-4f, -2.6f, 0f));
            s.Camera.Fov.Key(0f, 48f).Key(8f, 44f);
            s.Camera.Aperture.Key(0f, 11f);
            s.Camera.Life = 2.2f;
            s.Camera.Grade(4f, 14f, 8f, 0.2f, 0.7f);
        }

        /// <summary>Her photo of two takeaway cups side by side on the railing, the sun going down behind them: one
        /// of them is his.</summary>
        static void MediaBench(FilmStage s)
        {
            var set = FilmSets.Waterfront(s);
            FilmProps.PaperCup(s, new Vector3(0.5f, 1.088f, 2.15f));
            FilmProps.PaperCup(s, new Vector3(0.63f, 1.088f, 2.15f));
            s.Camera.Position.Key(0f, 0.44f, 1.19f, 1.42f);
            s.Camera.Target.Key(0f, 0.6f, 1.16f, 2.15f);
            s.Camera.Focus.Key(0f, 0.56f, 1.14f, 2.15f);
            s.Camera.Fov.Key(0f, 44f);
            s.Camera.Aperture.Key(0f, 4f);
            s.Camera.Exposure.Key(0f, 0.25f);
            s.Camera.Life = 0f;
            s.Camera.Grade(4f, 14f, 8f, 0.2f, 0.7f);
        }

        /// <summary>Her photo of the sky over the water, the railing along the bottom.</summary>
        static void MediaSky(FilmStage s)
        {
            var set = FilmSets.Waterfront(s);
            var sun = new Vector3(0.6f, 1.5f, 1.2f) + set.SunDirection * 60f;
            s.Camera.Position.Key(0f, 0.6f, 1.5f, 1.2f);
            s.Camera.Target.Key(0f, sun + new Vector3(5f, 4f, 0f));
            s.Camera.Fov.Key(0f, 52f);
            s.Camera.Aperture.Key(0f, 11f);
            s.Camera.Life = 0f;
            s.Camera.Grade(4f, 14f, 8f, 0.2f, 0.7f);
        }
    }
}
