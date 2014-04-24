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
 * BUGI: Jostain syystä pelaaja ei spawnaa pelaajaspawnivärillä osoitetulle paikalle, vaan oletuskohtaan (0,0). Selvitä miksi.
 * TODO: Kokeile toimiiko dynaaminen lataaminen
 *          - hahmoille,
 *          - esineille ja
 *          - maastolle.
 * TODO: Tee lataaja Apurit luokkaan musiikille ja äänitehosteille.
 * TODO: Lisää tapahtumakäsittelijät, jotka liipaisevat Tapahtuma:n (joka toistaa satunnaisen aiheeseen sopivan äänitehosteen)
 * TODO: Soita musiikkikipaleita satunnaisessa järjestyksessä peräperää.
 * TODO: Valikot,
 *          - alkuvalikko, josta valitaan "Satunnainen peli", "Valikoitu peli" (ks alla.), "Lopeta".
 *          - "Valikoitu peli"-valikko, joilla voi (alivalikoista) valita kentän, pelihahmon ja taustamusiikin.
 *             Tee myös pikanäppäimet, joilla pelin aikana voi vaihtaa näitä.
 * TODO: Toteuta pelaajahahmon lyöntitoiminto ja hyppytoiminto. Hypätessä voit välttää törmäyksen maaston (mutta ei esteiden) kanssa.
 * TODO: Harkitse simppelin inventaariosysteemin toteuttamista (numeroilla 1-9 valitaan mikä on kädessä, noukkia voi aseita, avaimia jne)
 * TODO: Lisää Xbox controller näpylät.
 */

public class PajaPeli : PhysicsGame
{
    enum Tapahtuma
	{
        Iskee,
        Kuolee,
        Tormaa,
        Noukkii,
        Sattuu
	}

    // Nämä ovat peliin liittyviä vakioita. 
    //  - näitä voi kokeilla muuttaa
    static int PELAAJAN_KAVELYNOPEUS = 1000;
    static double PELAAJAN_LIUKUMISVAKIO = 0.1;
    static int ESTE_TUMMA_VARI_RAJAARVO = 128;
    static Color PELAAJAN_ALOITUSPAIKAN_VARI = Color.FromPaintDotNet(0, 14);
    //  - näitä taas ei kannata kokeilla muuttaa
    static int RUUDUN_KUVAN_LEVEYS = 32;
    static int RUUDUN_KUVAN_KORKEUS = 32;
    static int RUUDUN_LEVEYS = 64;
    static int RUUDUN_KORKEUS = 64;

    static int KARTAN_MAKSIMILEVEYS = 100;
    static int KARTAN_MAKSIMIKORKEUS = 100;

    // Nämä pitävät sisällään peliin tiedostoista ladattavaa sisältöä.
    //  Dictionary tarkoittaa hakemistoa, jossa kuhunkin arvoon (esim. väri Color) on 
    //  linkitetty esim. lista kuvia (Image).
    Dictionary<Image, string> Nimet = new Dictionary<Image,string>();
    Dictionary<Color, List<Image>> HahmoKuvat;
    Dictionary<Color, List<Image>> MaastoKuvat;
    Dictionary<Color, List<Image>> EsineKuvat;
    List<Image> Kartat;
    Dictionary<Tapahtuma, List<SoundEffect>> Tehosteet;
    List<SoundEffect> Musiikki;

    // Pelin tilanne ja tilatietoa tallentavat muuttujat
    Image ValittuKartta;
    List<Vector> PelaajanAloitusPaikat = new List<Vector>();
    PhysicsObject Pelaaja;

    List<PhysicsObject> Hahmot = new List<PhysicsObject>();
    List<GameObject> Esineet = new List<GameObject>();

    public override void Begin()
    {        
        // Ladataan peliin lisätty sisältö (taikuutta tapahtuu Apurit-luokassa)
        Apurit.LataaKuvatKansiosta(@"DynamicContent\Hahmot", RUUDUN_KUVAN_LEVEYS, RUUDUN_KUVAN_KORKEUS, this, ref Nimet, out HahmoKuvat);
        Apurit.LataaKuvatKansiosta(@"DynamicContent\Maasto", RUUDUN_KUVAN_LEVEYS, RUUDUN_KUVAN_KORKEUS, this, ref Nimet, out MaastoKuvat);
        Apurit.LataaKuvatKansiosta(@"DynamicContent\Esineet", RUUDUN_KUVAN_LEVEYS, RUUDUN_KUVAN_KORKEUS, this, ref Nimet, out EsineKuvat);
        Apurit.LataaKartatKansiosta(@"DynamicContent\Kartat", KARTAN_MAKSIMILEVEYS, KARTAN_MAKSIMIKORKEUS, this, ref Nimet, out Kartat);        

        // TODO: Tee lataaja äänille ja lataa
        //Tehosteet = LataaAanetKansiosta(@"DynamicContent\Tehosteet");
        //Musiikki = LataaAanetKansiosta(@"DynamicContent\Musiikki");
        
        // TODO: Käynnistä taustamusiikki, kun se loppuu kutsu aliohjelmaa, joka käynnistää 

        // TODO: Näytä valikko, jolla voi valita pelaajahahmon.
        // TODO: Näytä valikko, jolla voi valita kentän.

        ValittuKartta = RandomGen.SelectOne<Image>(Kartat);
        LisaaPelaajaPeliin();
        LataaKentta(ValittuKartta);

        PhoneBackButton.Listen(ConfirmExit, "Lopeta peli");
        Keyboard.Listen(Key.Escape, ButtonState.Pressed, ConfirmExit, "Lopeta peli");
        Keyboard.Listen(Key.Left,   ButtonState.Down, LiikutaPelaajaa, null, new Vector( -PELAAJAN_KAVELYNOPEUS, 0 ));
        Keyboard.Listen(Key.Right,  ButtonState.Down, LiikutaPelaajaa, null, new Vector( PELAAJAN_KAVELYNOPEUS, 0 ));
        Keyboard.Listen(Key.Up,     ButtonState.Down, LiikutaPelaajaa, null, new Vector( 0, PELAAJAN_KAVELYNOPEUS ));
        Keyboard.Listen(Key.Down,   ButtonState.Down, LiikutaPelaajaa, null, new Vector( 0, -PELAAJAN_KAVELYNOPEUS ));

        // TODO: Tee näppäinkuuntelijat, joilla voi valita kentän, pelaajahahmon, taustamusan
        Apurit.VaihdaKokoruuduntilaan(this.Window.Handle, true);

        Timer.SingleShot(0.1, TeeLoppuSilausPelille);
    }

    void TeeLoppuSilausPelille()
    {
        // Siirrä pelaaja aloituspaikkaan. Ja tuo se näkyviin.
        if (PelaajanAloitusPaikat.Count == 0)
            PelaajanAloitusPaikat.Add(new Vector(0, 0));
        Vector paikka = RandomGen.SelectOne<Vector>(PelaajanAloitusPaikat);
        Pelaaja.Position = paikka;
        Pelaaja.IsVisible = true;
        Pelaaja.IgnoresCollisionResponse = false;

        // Lisää maastoa esineiden ja hahmojen alle
        foreach (GameObject esine in Esineet)
        {
            LisaaTaustaMaasto(esine);
        }
        foreach (GameObject hahmo in Hahmot)
        {
            LisaaTaustaMaasto(hahmo);

            // Pistä samalla viholliset liikkeelle
            // Et ole itsesi vihollinen (hyppää yli)
            if (hahmo != Pelaaja)
            {
                RandomMoverBrain aivot = new RandomMoverBrain(PELAAJAN_KAVELYNOPEUS / 10);
                aivot.TurnWhileMoving = true;
                hahmo.Brain = aivot;
            }
        }
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
        GameObject uusiMaasto = new GameObject(maasto.Width, maasto.Height);
        uusiMaasto.Color = maasto.Color;
        uusiMaasto.Image = maasto.Image;
        uusiMaasto.Tag = maasto.Tag;
        uusiMaasto.Position = esineTaiHahmo.Position;
        Add(uusiMaasto);
    }

    void LiikutaPelaajaa(Vector vektori)
    {
        Pelaaja.Push(vektori);
        Pelaaja.Angle = Pelaaja.Velocity.Angle; // Käännä pelaajaa
    }
    
    void LataaKentta(Image karttaKuva)
    {
        //1. Luetaan kuva uuteen ColorTileMappiin, kuvan nimen perässä ei .png-päätettä.
        ColorTileMap ruudut = new ColorTileMap( karttaKuva );
  
        //2. Kerrotaan mitä aliohjelmaa kutsutaan, kun tietyn värinen pikseli tulee vastaan kuvatiedostossa.
        HashSet<Color> varatutVarit = new HashSet<Color>();
        Apurit.LisaaKasittelijatVareille(new List<Color>(MaastoKuvat.Keys), ruudut, LisaaMaastoaKartalle, ref varatutVarit);
        Apurit.LisaaKasittelijatVareille(new List<Color>(HahmoKuvat.Keys), ruudut, LisaaHahmoKartalle, ref varatutVarit);
        Apurit.LisaaKasittelijatVareille(new List<Color>(EsineKuvat.Keys), ruudut, LisaaEsineKartalle, ref varatutVarit);
        // Loput värit tulkitaan maastoksi
        Apurit.LisaaKasittelijatVareille(Apurit.AnnaKaikkiKuvanVarit(karttaKuva), ruudut, LisaaMaastoaKartalle, ref varatutVarit);
        
        //3. Execute luo kentän
        //   Parametreina leveys ja korkeus
        ruudut.Execute(RUUDUN_LEVEYS+1, RUUDUN_KORKEUS+1);
    }

#region PeliOlioidenLisääminen
    void LisaaMaastoaKartalle(Vector paikka, double leveys, double korkeus, Color vari)
    {
        // Tumma väri, ei voi läpäistä.
        GameObject maastoOlio = null;
        if (vari.RedComponent+vari.BlueComponent+vari.GreenComponent < ESTE_TUMMA_VARI_RAJAARVO)
        {
            PhysicsObject este = PhysicsObject.CreateStaticObject(leveys, korkeus);
            maastoOlio = este;
            Add(maastoOlio, -1);
        }
        // Vaalea väri, ihan vaan taustaa
        else
        {
            GameObject tausta = new GameObject(leveys, korkeus);
            maastoOlio = tausta;
            Add(maastoOlio, -2);
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
            PhysicsObject hahmo = new PhysicsObject(leveys, korkeus);
            hahmo.Position = paikka;
            hahmo.Image = RandomGen.SelectOne<Image>(HahmoKuvat[vari]);
            hahmo.Tag = Nimet[hahmo.Image];
            Add(hahmo, 2);
            Hahmot.Add(hahmo);
        }
    }
    void LisaaEsineKartalle( Vector paikka, double leveys, double korkeus, Color vari)
    {
        PhysicsObject esine = new PhysicsObject(leveys, korkeus);
        esine.Image = RandomGen.SelectOne<Image>(EsineKuvat[vari]);
        esine.Tag = Nimet[esine.Image];
        Add(esine, 1);
        Esineet.Add(esine);

        // Kun pelaaja osuu esineeseen, kutsutaan EsineKerattu aliohjelmaa
        AddCollisionHandler(Pelaaja, EsineKerattu);
    }
    void LisaaPelaajaPeliin()
    {
        Pelaaja = new PhysicsObject(RUUDUN_LEVEYS-2, RUUDUN_KORKEUS-2);
        Pelaaja.LinearDamping = 1-PELAAJAN_LIUKUMISVAKIO;
        Pelaaja.IsVisible = false;
        Pelaaja.IgnoresCollisionResponse = true;
        Add(Pelaaja, 2);
        Hahmot.Add(Pelaaja);

        if (HahmoKuvat.ContainsKey(PELAAJAN_ALOITUSPAIKAN_VARI))
        {
            Pelaaja.Image = RandomGen.SelectOne<Image>(HahmoKuvat[PELAAJAN_ALOITUSPAIKAN_VARI]);
        }
        else
        {
            Pelaaja.Shape = Shape.Circle;
            Pelaaja.Color = PELAAJAN_ALOITUSPAIKAN_VARI;
        }
        Camera.Follow(Pelaaja);
    }
#endregion

#region PeliTapahtumienKäsittely
    void EsineKerattu(PhysicsObject pelaaja, PhysicsObject esine)
    {
        ToistaTehoste(Tapahtuma.Noukkii);
        // TODO: Mitä sitten tapahtuu? Kirjoita koodia tähän...
    }
    void OsuuHahmoon(PhysicsObject pelaaja, PhysicsObject hahmo)
    {
        ToistaTehoste(Tapahtuma.Kuolee);
        pelaaja.Destroy();
        // TODO: Mitä sitten tapahtuu? Kirjoita koodia tähän...
    }
    void ToistaTehoste(Tapahtuma tapahtuma)
    {
        if (Tehosteet.ContainsKey(tapahtuma))
        {
            SoundEffect tehoste = RandomGen.SelectOne<SoundEffect>(Tehosteet[tapahtuma]);
            tehoste.Play();
        }
    }
#endregion
}
