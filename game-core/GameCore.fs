﻿namespace GameCore

open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics;
open Microsoft.Xna.Framework.Input;
open System

type Resolution =
| Windowed of int * int
| FullScreen of int * int

type private Content =
| TextureAsset of Texture2D
| FontAsset of SpriteFont

type GameCore<'TModel> (resolution, assetsToLoad, updateModel, getView)
    as this = 
    inherit Game()

    let mutable graphics = new GraphicsDeviceManager(this)

    let mutable assets = Map.empty<string, Content>

    let mutable keyboardInfo = { pressed = []; keysDown = []; keysUp = [] }
    let mutable currentModel: 'TModel option = None
    let mutable currentView: Drawable list = []

    let mutable spriteBatch = Unchecked.defaultof<SpriteBatch>

    do 
        match resolution with
        | FullScreen (w,h) -> 
            graphics.PreferredBackBufferWidth <- w
            graphics.PreferredBackBufferHeight <- h
            graphics.IsFullScreen <- true
        | Windowed (w,h) -> 
            graphics.PreferredBackBufferWidth <- w
            graphics.PreferredBackBufferHeight <- h

    let updateKeyboardInfo (keyboard: KeyboardState) (existing: KeyboardInfo) =
        let pressed = keyboard.GetPressedKeys() |> Set.ofArray
        {
            pressed = pressed |> Set.toList
            keysDown = Set.difference pressed (existing.pressed |> Set.ofList) |> Set.toList
            keysUp = Set.difference (existing.pressed |> Set.ofList) pressed |> Set.toList
        }

    let getMouseInfo (mouse: MouseState) =
        {
            position = mouse.X, mouse.Y
            pressed = mouse.LeftButton = ButtonState.Pressed, mouse.RightButton = ButtonState.Pressed
        }

    let asVector2 (x,y) = new Vector2(float32 x, float32 y)
    let asRectangle (x,y,width,height) = 
        new Rectangle (x,y,width,height)
    
    let drawImage (spriteBatch: SpriteBatch) image colour = 
        let sourceRect = 
            match image.sourceRect with 
            | None -> Unchecked.defaultof<Nullable<Rectangle>> 
            | Some r -> asRectangle r |> Nullable
        let texture =
            match Map.tryFind image.assetKey assets with
            | Some (TextureAsset t) -> t
            | None -> sprintf "Missing asset: %s" image.assetKey |> failwith
            | _-> sprintf "Asset was not a Texture2D: %s" image.assetKey |> failwith
        spriteBatch.Draw(
            texture, asRectangle image.destRect, 
            sourceRect, colour, 0.0f, Vector2.Zero, 
            SpriteEffects.None, 0.0f)
    
    let drawText (spriteBatch: SpriteBatch) text colour =
        let font =
            match Map.tryFind text.assetKey assets with
            | Some (FontAsset f) -> f
            | None -> sprintf "Missing asset: %s" text.assetKey |> failwith
            | _-> sprintf "Asset was not a SpriteFont: %s" text.assetKey |> failwith
        let position =
            match text.origin with
            | TopLeft -> asVector2 text.position
            | Centre -> 
                let size = Vector2.Divide (font.MeasureString(text.text), 2.f / float32 text.scale)
                Vector2.Subtract (asVector2 text.position, size)
        spriteBatch.DrawString(
            font, text.text, position, colour, 
            0.0f, Vector2.Zero, float32 text.scale, SpriteEffects.None, 0.5f)

    override __.LoadContent() = 
        spriteBatch <- new SpriteBatch(this.GraphicsDevice)
        assets <- 
            assetsToLoad
            |> List.map (fun a ->
                match a with
                | Texture info -> info.key, this.Content.Load<Texture2D>(info.path) |> TextureAsset
                | Font info -> info.key, this.Content.Load<SpriteFont>(info.path) |> FontAsset)
            |> Map.ofList

    override __.Update(gameTime) =
        keyboardInfo <- updateKeyboardInfo (Keyboard.GetState()) keyboardInfo
        let mouseInfo = getMouseInfo (Mouse.GetState())
        let runState = { 
            elapsed = gameTime.TotalGameTime.TotalMilliseconds 
            keyboard = keyboardInfo
            mouse = mouseInfo
        }
        
        currentModel <- updateModel runState currentModel
        match currentModel with
        | None -> __.Exit()
        | Some model ->
            currentView <- getView runState model

    override __.Draw(_) =
        this.GraphicsDevice.Clear Color.White
        
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp)

        currentView
            |> List.iter (fun drawable -> 
                match drawable with 
                | Image i -> drawImage spriteBatch i Color.White
                | ColouredImage (c,i) -> drawImage spriteBatch i c
                | Text t -> drawText spriteBatch t Color.Black
                | ColouredText (c,t) -> drawText spriteBatch t c)

        spriteBatch.End()