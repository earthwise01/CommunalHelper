local drawableSprite = require("structs.drawable_sprite")
local drawableLine = require("structs.drawable_line")
local drawableNinePatch = require("structs.drawable_nine_patch")
local drawableRectangle = require("structs.drawable_rectangle")
local utils = require("utils")
local communalHelper = require("mods").requireFromPlugin("libraries.communal_helper")

local dashZipMover = {}

local function themeTextures(entity)
    local prefix = entity.spritePath or "objects/CommunalHelper/strawberryJam/dashZipMover/"
    return {
        nodeCog = prefix .. "cog",
        lights = prefix .. "light01",
        block = prefix .. "block"
    }
end

local blockNinePatchOptions = {
    mode = "border",
    borderMode = "repeat"
}

local centerColor = {0, 0, 0}
local defaultRopeColor = "065217"

dashZipMover.name = "CommunalHelper/SJ/DashZipMover"
dashZipMover.depth = -9999
dashZipMover.nodeVisibility = "never"
dashZipMover.nodeLimits = {1, -1}
dashZipMover.minimumSize = {16, 16}
dashZipMover.placements = {
    name = "main",
    data = {
        width = 16,
        height = 16,
        spritePath = "objects/CommunalHelper/strawberryJam/dashZipMover/",
        drawBlackBorder = false,
        ropeColor = "046e19",
        ropeLightColor = "329415",
        ropeShadowColor = "003622",
        soundEvent = "event:/CommunalHelperEvents/game/strawberryJam/game/dash_zip_mover/zip_mover",
        slow = false,
        permanent = false,
        waiting = false,
        linked = false
    }
}

dashZipMover.fieldInformation = {
    ropeColor = {
        fieldType = "color"
    },
    ropeLightColor = {
        fieldType = "color"
    },
    ropeShadowColor = {
        fieldType = "color"
    }
}

local function addBlockSprites(sprites, entity, blockTexture, lightsTexture, x, y, width, height)
    local rectangle = drawableRectangle.fromRectangle("fill", x + 2, y + 2, width - 4, height - 4, centerColor)

    local frameNinePatch = drawableNinePatch.fromTexture(blockTexture, blockNinePatchOptions, x, y, width, height)
    local frameSprites = frameNinePatch:getDrawableSprite()

    local lightsSprite = drawableSprite.fromTexture(lightsTexture, entity)

    lightsSprite:addPosition(math.floor(width / 2), 0)
    lightsSprite:setJustification(0.5, 0.0)

    if entity.drawBlackBorder then
        local outlineRect = drawableRectangle.fromRectangle("fill", x - 1, y - 1, width + 2, height + 2, {0, 0, 0, 1})
        outlineRect.depth = 5000
        table.insert(sprites, outlineRect)
    end

    table.insert(sprites, rectangle:getDrawableSprite())

    for _, sprite in ipairs(frameSprites) do
        table.insert(sprites, sprite)
    end

    table.insert(sprites, lightsSprite)
end

function dashZipMover.sprite(room, entity)
    local sprites = {}

    local x, y = entity.x or 0, entity.y or 0
    local width, height = entity.width or 16, entity.height or 16
    local halfWidth, halfHeight = math.floor(entity.width / 2), math.floor(entity.height / 2)

    local nodes = entity.nodes or {{x = 0, y = 0}}
    local cogSprite = themeTextures(entity).nodeCog
    local ropeColor = entity.ropeColor or defaultRopeColor

    local nodeSprites = communalHelper.getZipMoverNodeSprites(x, y, width, height, nodes, cogSprite, {1, 1, 1}, ropeColor)
    for _, sprite in ipairs(nodeSprites) do
        table.insert(sprites, sprite)
    end

    addBlockSprites(sprites, entity, themeTextures(entity).block, themeTextures(entity).lights, x, y, width, height)

    return sprites
end

function dashZipMover.selection(room, entity)
    local x, y = entity.x or 0, entity.y or 0
    local width, height = entity.width or 8, entity.height or 8
    local halfWidth, halfHeight = math.floor(entity.width / 2), math.floor(entity.height / 2)

    local mainRectangle = utils.rectangle(x, y, width, height)

    local cogSprite = drawableSprite.fromTexture(themeTextures(entity).nodeCog, entity)
    local cogWidth, cogHeight = cogSprite.meta.width, cogSprite.meta.height

    local nodes = entity.nodes or {{x = 0, y = 0}}
    local nodeRectangles = {}
    for _, node in ipairs(nodes) do
        local centerNodeX, centerNodeY = node.x + halfWidth, node.y + halfHeight

        table.insert(nodeRectangles, utils.rectangle(centerNodeX - math.floor(cogWidth / 2), centerNodeY - math.floor(cogHeight / 2), cogWidth, cogHeight))
    end

    return mainRectangle, nodeRectangles
end

return dashZipMover