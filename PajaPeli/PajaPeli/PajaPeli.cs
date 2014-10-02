using System;
using System.Collections.Generic;
using Jypeli;
using Jypeli.Assets;
using Jypeli.Controls;
using Jypeli.Effects;
using Jypeli.Widgets;
using System.IO;

/*
 * TODO / BUGIT
 * 
 * TODO: Testaa toimiiko dynaaminen lataaminen
 *          - hahmoille,
 *          - esineille ja
 *          - maastolle.
 *          - äänille 
 * TODO: Testaa erityisesti liikkumisen ääni.
 * TODO: Jos valittavaa on liikaa, valikkoon ei mahdu!
 * TODO: Tee esineistä kerättäviä ja niille inventaario.
 * TODO: Valikot,
 *          - alkuvalikko, josta valitaan "Satunnainen peli", "Valikoitu peli" (ks alla.), "Lopeta".
 *          - "Valikoitu peli"-valikko, joilla voi (alivalikoista) valita kentän, pelihahmon ja taustamusiikin.
 *             Tee myös pikanäppäimet, joilla pelin aikana voi vaihtaa näitä.
 *             
 */

public class PajaPeli : TiedostoistaLadattavaPeli
{
    // Nämä ovat peliin liittyviä vakioita. 
    //  - näitä voi kokeilla muuttaa
    static double PELAAJAN_KAVELYNOPEUS = 1000;
    static double PELAAJAN_HYPPYKORKEUS = 2000;
    static double PELAAJAN_LIUKUMISVAKIO = 0.1;
    static int ESTE_TUMMA_VARI_RAJAARVO = 200;
    public static Color PELAAJAN_ALOITUSPAIKAN_VARI = Color.FromPaintDotNet(0, 14);
    public static Color KENTÄN_MAALIN_VARI = Color.FromPaintDotNet(1, 14);

    // Peliin valitut asiat
    public Image ValittuPelaajaHahmo;
    public Image ValittuKartta;
    public Image ValittuTausta;
    public SoundEffect ValittuMusiikki; 

    // Pelin tilanne ja tilatietoa tallentavat muuttujat
    List<Vector> PelaajanAloitusPaikat = new List<Vector>();
    PlatformCharacter Pelaaja = null;
    List<PlatformCharacter> PelissaOlevatHahmot = new List<PlatformCharacter>();
    List<GameObject> PelissaOlevatEsineet = new List<GameObject>();

    public override void Begin()
    {
        // Tätä aliohjelmaa kutsumalla peli lataa kaikki PajaPeliin tehdyt sisällöt levyltä.
        LataaSisallotTiedostoista();

        Mouse.IsCursorVisible = true;
        
        // Nollaa valinnat
        ValittuPelaajaHahmo = null;
        ValittuKartta = null;
        ValittuTausta = null;
        ValittuMusiikki = null; 

        Apuri.NaytaAlkuValikko();
    }

    /// <summary>
    /// Tätä kutsutaan kun pelaaja valitsee alkuvalikosta pelattavaksi satunnaisen pelin.
    /// </summary>
    public void SatunnainenPeliValittu()
    {
        // kuva,   
        ValittuPelaajaHahmo = null;
        if (HahmoKuvat.ContainsKey(PELAAJAN_ALOITUSPAIKAN_VARI))
        {
            ValittuPelaajaHahmo = RandomGen.SelectOne<Image>(HahmoKuvat[PELAAJAN_ALOITUSPAIKAN_VARI]);
        }
        LisaaPelaajaPeliin();

        ValittuKartta = RandomGen.SelectOne<Image>(Kartat);
        ValittuTausta = RandomGen.SelectOne<Image>(Taustakuvat);
        LataaKentta();

        // äänet, 
        SoitaSatunnainenTaustaMusiikki();
     
        // käy!
        Timer.SingleShot(0.1, KaynnistaPeli);
    }

    public void TiettyPeliValittu()
    {
        LisaaPelaajaPeliin();
        LataaKentta();
        if (ValittuMusiikki != null)
        {
            Sound biisi = ValittuMusiikki.CreateSound();
            biisi.IsLooped = true;
            biisi.Play();
        }
        Timer.SingleShot(0.1, KaynnistaPeli);
    }

    void LisaaNappainKuuntelijat()
    {
        Keyboard.Listen(Key.Escape, ButtonState.Pressed, ConfirmExit, "Lopeta peli");
        Keyboard.Listen(Key.Left, ButtonState.Down, LiikutaPelaajaa, "Liikuta pelaajaa nuolinäppäimillä", -PELAAJAN_KAVELYNOPEUS);
        Keyboard.Listen(Key.Right, ButtonState.Down, LiikutaPelaajaa, null, PELAAJAN_KAVELYNOPEUS);
        Keyboard.Listen(Key.Up, ButtonState.Down, HyppaytaPelaajaa, null, PELAAJAN_HYPPYKORKEUS);

        ControllerOne.Listen(Button.Back, ButtonState.Pressed, ConfirmExit, "Lopeta peli");
        ControllerOne.Listen(Button.DPadLeft, ButtonState.Down, LiikutaPelaajaa, "Liikuta pelaajaa XBox-ohjaimen ristikkonäppäimellä", -PELAAJAN_KAVELYNOPEUS);
        ControllerOne.Listen(Button.DPadRight, ButtonState.Down, LiikutaPelaajaa, null, PELAAJAN_KAVELYNOPEUS);
        ControllerOne.Listen(Button.A, ButtonState.Down, HyppaytaPelaajaa, "XBox-ohjaimen A-nappi on hyppynappi", PELAAJAN_HYPPYKORKEUS);

        ShowControlHelp();
    }

    void KaynnistaPeli()
    {
        LisaaNappainKuuntelijat();

        // Siirrä pelaaja johonkin aloituspaikkaan. Ja tuo se näkyviin.
        Vector paikka = RandomGen.SelectOne<Vector>(PelaajanAloitusPaikat);
        Pelaaja.Position = paikka;
        Pelaaja.IsVisible = true;
        Pelaaja.IgnoresCollisionResponse = false;

        // Lisää maastoa esineiden ja hahmojen alle
        foreach (GameObject esine in PelissaOlevatEsineet)
        {
            LisaaTaustaMaasto(esine);
        }
        foreach (GameObject hahmo in PelissaOlevatHahmot)
        {
            LisaaTaustaMaasto(hahmo);
            if (hahmo.Brain != null)
            {
                hahmo.Brain.Active = true;
            }
        }
        Mouse.IsCursorVisible = false;
        Gravity = new Vector(0, -3000);
    }
    
    void LataaKentta()
    {
        //1. Luetaan kuva uuteen ColorTileMappiin, kuvan nimen perässä ei .png-päätettä.
        ColorTileMap ruudut = new ColorTileMap( ValittuKartta );
  
        //2. Kerrotaan mitä aliohjelmaa kutsutaan, kun tietyn värinen pikseli tulee vastaan kuvatiedostossa.
        HashSet<Color> varatutVarit = new HashSet<Color>();
        Apuri.LisaaKasittelijatVareille(new List<Color>() {KENTÄN_MAALIN_VARI}, ruudut, LisaaMaaliKartalle, ref varatutVarit);
        Apuri.LisaaKasittelijatVareille(new List<Color>(MaastoKuvat.Keys), ruudut, LisaaMaastoaKartalle, ref varatutVarit);
        Apuri.LisaaKasittelijatVareille(new List<Color>(HahmoKuvat.Keys), ruudut, LisaaHahmoKartalle, ref varatutVarit);
        Apuri.LisaaKasittelijatVareille(new List<Color>(EsineKuvat.Keys), ruudut, LisaaEsineKartalle, ref varatutVarit);
        // Loput värit tulkitaan maastoksi
        Apuri.LisaaKasittelijatVareille(Apuri.AnnaKaikkiKuvanVarit(ValittuKartta), ruudut, LisaaMaastoaKartalle, ref varatutVarit);
        
        //3. Execute luo kentän
        //   Parametreina leveys ja korkeus
        ruudut.Execute(RUUDUN_LEVEYS+1, RUUDUN_KORKEUS+1);

        // Pelaaja aloittaa keskeltä, jos ei ole merkattuja aloituspaikkoja
        if (PelaajanAloitusPaikat.Count == 0)
            PelaajanAloitusPaikat.Add(new Vector(0, 0));

        // Aseta taustakuva
        if (ValittuTausta != null)
        {
            Level.Background.Image = ValittuTausta;
            Level.Background.ScaleToLevel();
            Level.Background.TileToLevel();
        }
    }


    #region PeliTapahtumienKäsittely
    void PelaajaKeraaEsineen(PhysicsObject pelaaja, PhysicsObject esine)
    {
        ToistaTehoste(Tapahtuma.Noukkii);
        esine.Destroy();
        // TODO: Mitä sitten tapahtuu? Kirjoita koodia tähän...
    }
    void PelaajaOsuuHahmoon(PhysicsObject pelaaja, PhysicsObject hahmo)
    {
        ToistaTehoste(Tapahtuma.Kuolee);
        pelaaja.Destroy(); // Tapa pelaaja
        Timer.SingleShot(0.5, PeliLoppuu);
        // TODO: Mitä sitten tapahtuu? Kirjoita koodia tähän...
    }
    void PelaajaPaasiMaaliin(PhysicsObject pelaaja, PhysicsObject maali)
    {
        // Aloita alusta, TODO: tai tee jotain muuta...
        ToistaTehoste(Tapahtuma.Voittaa);
        pelaaja.Destroy();

        Timer.SingleShot(0.5, PeliLoppuu);
    }
    void PelaajaOsuuEsteeseen(PhysicsObject pelaaja, PhysicsObject este)
    {
        ToistaTehoste(Tapahtuma.Sattuu);
        // TODO: Mitä sitten tapahtuu? Kirjoita koodia tähän...
    }

    void PeliLoppuu()
    {
        Label loppu = new Label("GAME OVER (paina ESC)\ntai odota...");
        loppu.Font = Font.DefaultLargeBold;
        Add(loppu);
        ToistaTehoste(Tapahtuma.PeliLoppuu);

        Timer.SingleShot(2, AloitaAlusta);
    }
    void AloitaAlusta()
    {
        ClearAll();
        Begin();
    }
    #endregion

    #region NapinPainallustenKäsittely
    void LiikutaPelaajaa(double nopeus)
    {
        ToistaTehoste(Tapahtuma.Liikkuu);
        Pelaaja.Walk(nopeus);
    }

    void HyppaytaPelaajaa(double korkeus)
    {
        ToistaTehoste(Tapahtuma.Hyppaa);
        Pelaaja.Jump(korkeus);
    }
    #endregion

#region PeliOlioidenLisääminen
    void LisaaMaaliKartalle(Vector paikka, double leveys, double korkeus, Color vari)
    {
        PhysicsObject maali = PhysicsObject.CreateStaticObject(leveys, korkeus);
        maali.IgnoresCollisionResponse = true;
        maali.CollisionIgnoreGroup = 1; // Suorituskykyoptimointi
        maali.Position = paikka;
        maali.Color = vari;
        Add(maali, -1);

        // Kun pelaaja koskettaa maalia, kutsutaan PelaajaPaasiMaaliin aliohjelmaa
        AddCollisionHandler(Pelaaja, maali, PelaajaPaasiMaaliin);

        // Aseta kuva, jos sellainen on
        if (MaastoKuvat.ContainsKey(vari))
        {
            maali.Image = RandomGen.SelectOne<Image>(MaastoKuvat[vari]);
            maali.Tag = Nimet[maali.Image];
        }
        else if (EsineKuvat.ContainsKey(vari))
        {
            maali.Image = RandomGen.SelectOne<Image>(EsineKuvat[vari]);
            maali.Tag = Nimet[maali.Image];
        }
    }
    void LisaaMaastoaKartalle(Vector paikka, double leveys, double korkeus, Color vari)
    {
        // Tumma väri, ei voi läpäistä.
        GameObject maastoOlio = null;
        if (vari.RedComponent+vari.BlueComponent+vari.GreenComponent < ESTE_TUMMA_VARI_RAJAARVO)
        {
            PhysicsObject este = PhysicsObject.CreateStaticObject(leveys, korkeus);
            maastoOlio = este;
            este.CollisionIgnoreGroup = 1; // Suorituskykyoptimointi
            Add(este, -1);

            // Kun pelaaja osuu esteeseen, kutsutaan PelaajaOsuuEsteeseen aliohjelmaa
            AddCollisionHandler(Pelaaja, este, PelaajaOsuuEsteeseen);
        }
        // Vaalea väri, ihan vaan taustaa (ei törmäyksiä)
        else
        {
            GameObject tausta = new GameObject(leveys, korkeus);
            maastoOlio = tausta;
            Add(tausta, -2);
        }

        maastoOlio.Color = vari;
        maastoOlio.Position = paikka;

        // Aseta kuva, jos sellainen on
        if (MaastoKuvat.ContainsKey(vari))
        {
            maastoOlio.Image = RandomGen.SelectOne<Image>(MaastoKuvat[vari]);
            maastoOlio.Tag = Nimet[maastoOlio.Image];
        }
    }
    void LisaaHahmoKartalle(Vector paikka, double leveys, double korkeus, Color vari)
    {
        // Magneta on pelaaja
        if (vari == PELAAJAN_ALOITUSPAIKAN_VARI)
        {
            PelaajanAloitusPaikat.Add(paikka);
        }
        else
        {
            PlatformCharacter hahmo = new PlatformCharacter(leveys, korkeus);
            hahmo.Position = paikka;
            hahmo.Image = RandomGen.SelectOne<Image>(HahmoKuvat[vari]);
            hahmo.Tag = Nimet[hahmo.Image];
            Add(hahmo, 2);
            PelissaOlevatHahmot.Add(hahmo);

            // Lisää vihollisille aivot
            PlatformWandererBrain aivot = new PlatformWandererBrain();
            aivot.Speed = PELAAJAN_KAVELYNOPEUS / 4;
            aivot.FallsOffPlatforms = false;
            aivot.TriesToJump = false;
            aivot.Active = false; // pistetään päälle kun peli alkaa
            hahmo.Brain = aivot;

            // Kun pelaaja osuu hahmoon, kutsutaan PelaajaOsuuHahmoon aliohjelmaa
            AddCollisionHandler(Pelaaja, hahmo, PelaajaOsuuHahmoon);
        }
    }
    void LisaaEsineKartalle( Vector paikka, double leveys, double korkeus, Color vari)
    {
        PhysicsObject esine = new PhysicsObject(leveys-2, korkeus-2);
        esine.Image = RandomGen.SelectOne<Image>(EsineKuvat[vari]);
        esine.Tag = Nimet[esine.Image];
        esine.Position = paikka;
        Add(esine, 1);
        PelissaOlevatEsineet.Add(esine);
        AddCollisionHandler(Pelaaja, esine, PelaajaKeraaEsineen);
    }
    void LisaaPelaajaPeliin()
    {
        Pelaaja = new PlatformCharacter(RUUDUN_LEVEYS - 2, RUUDUN_KORKEUS - 2);
        Pelaaja.LinearDamping = 1-PELAAJAN_LIUKUMISVAKIO;
        Pelaaja.IsVisible = false;
        Pelaaja.IgnoresCollisionResponse = true;
        Add(Pelaaja, 2);
        PelissaOlevatHahmot.Add(Pelaaja);

        if (ValittuPelaajaHahmo!=null)
        {
            Pelaaja.Image = ValittuPelaajaHahmo;
            Pelaaja.Tag = Nimet[ValittuPelaajaHahmo];
        }
        else
        {
            Pelaaja.Shape = Shape.Circle;
            Pelaaja.Color = PELAAJAN_ALOITUSPAIKAN_VARI;
        }
        Camera.Follow(Pelaaja);
    }
    void LisaaTaustaMaasto(GameObject esineTaiHahmo)
    {
        GameObject maasto = null;
        foreach (GameObject lahin in GetObjectsAt(esineTaiHahmo.Position, esineTaiHahmo.Width * 1.5))
        {
            // On esine itse tai este (tai hahmo)
            if (lahin == esineTaiHahmo || lahin is PhysicsObject)
                continue;
            maasto = lahin;
            break; // Lopettaa etsinnän
        }
        if (maasto != null)
        {
            GameObject uusiMaasto = new GameObject(maasto.Width, maasto.Height);
            uusiMaasto.Color = maasto.Color;
            uusiMaasto.Image = maasto.Image;
            uusiMaasto.Tag = maasto.Tag;
            uusiMaasto.Position = esineTaiHahmo.Position;
            Add(uusiMaasto);
        }
    }
#endregion
}
