using System;
using UnityEngine;

/// <summary>
/// Source of truth for player currencies (gems, coins if needed).
/// - Lives in scene on a GameObject (use a persistent Managers object).
/// - Provides safe Add / TrySpend methods.
/// - Fires OnCurrencyChanged on any change.
/// </summary>
public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance { get; private set; }

    [Header("Starting Balances (only used if no save is loaded)")]
    [Min(0)] [SerializeField] private int startingGems = 10;
    [Min(0)] [SerializeField] private int startingCoins = 10;
    [Min(0)] [SerializeField] private int startingHeroXP = 0;   // NEW

    public int StartingCoins => startingCoins; // <— add
    public int StartingGems => startingGems;  // <— add
    public int StartingHeroXP => startingHeroXP;                // NEW


    // Current balances
    public int Gems { get; private set; }
    public int Coins { get; private set; }
    public int HeroXP { get; private set; }                     // NEW


    /// <summary>
    /// Invoked whenever a currency value changes.
    /// Args: (currencyName, newValue, delta)
    /// </summary>
    public event Action<string, int, int> OnCurrencyChanged;

    private void Awake()
    {
        // Singleton 
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // If a save system sets balances later, it should call SetGems/SetCoins.
        Gems = startingGems;
        Coins = startingCoins;
        HeroXP = startingHeroXP;    // NEW

    }

    #region Public API (Gems)

    public void AddGems(int delta)
    {
        if (delta == 0) return;
        int old = Gems;
        Gems = Mathf.Max(0, Gems + delta);

        SaveSystem.SetGems(Gems);   // NEW – persist
        FireChanged("Gems", Gems, Gems - old);
    }

    public bool TrySpendGems(int amount)
    {
        if (amount <= 0) return true;
        if (Gems < amount) return false;

        int old = Gems;
        Gems -= amount;

        SaveSystem.SetGems(Gems);   // NEW – persist
        FireChanged("Gems", Gems, Gems - old);
        return true;
    }

    public void SetGems(int absolute)
    {
        int val = Mathf.Max(0, absolute);
        if (val == Gems) return;
        int old = Gems;
        Gems = val;

        SaveSystem.SetGems(Gems);   // NEW – persist
        FireChanged("Gems", Gems, Gems - old);
    }

    #endregion

    #region Public API (Coins - optional for future)

    public void AddCoins(int delta)
    {
        if (delta == 0) return;
        int old = Coins;
        Coins = Mathf.Max(0, Coins + delta);
        SaveSystem.SetCoins(Coins);            // <— persist

        FireChanged("Coins", Coins, Coins - old);
    }

    public bool TrySpendCoins(int amount)
    {
        if (amount <= 0) return true;
        if (Coins < amount) return false;

        int old = Coins;
        Coins -= amount;
        SaveSystem.SetCoins(Coins);            // <— persist

        FireChanged("Coins", Coins, Coins - old);
        return true;
    }

    public void SetCoins(int val, bool silent = false)
    {
        if (val == Coins) return;
        int old = Coins;
        Coins = Mathf.Max(0, val);
        SaveSystem.SetCoins(Coins);            // <— persist
        if (!silent) FireChanged("Coins", Coins, Coins - old);
    }

    #endregion

    // ------------- Hero XP (NEW) -------------

    public void AddHeroXP(int delta)
    {
        if (delta == 0) return;
        int old = HeroXP;
        HeroXP = Mathf.Max(0, HeroXP + delta);

        SaveSystem.SetHeroXP(HeroXP);       // NEW – persist
        FireChanged("HeroXP", HeroXP, HeroXP - old);
    }

    // rarely needed, but you might consume XP elsewhere
    public bool TrySpendHeroXP(int amount)
    {
        if (amount <= 0) return true;
        if (HeroXP < amount) return false;

        int old = HeroXP;
        HeroXP -= amount;

        SaveSystem.SetHeroXP(HeroXP);       // NEW – persist
        FireChanged("HeroXP", HeroXP, HeroXP - old);
        return true;
    }

    public void SetHeroXP(int absolute)
    {
        int val = Mathf.Max(0, absolute);
        if (val == HeroXP) return;
        int old = HeroXP;
        HeroXP = val;

        SaveSystem.SetHeroXP(HeroXP);       // NEW – persist
        FireChanged("HeroXP", HeroXP, HeroXP - old);
    }

    private void FireChanged(string currency, int newValue, int delta)
    {
        try { OnCurrencyChanged?.Invoke(currency, newValue, delta); }
        catch (Exception e) { Debug.LogException(e, this); }
    }
}
