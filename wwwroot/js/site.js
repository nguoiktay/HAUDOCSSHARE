document.addEventListener("DOMContentLoaded", function () {
    const themeToggle = document.getElementById("themeToggle");
    if (themeToggle) {
        const icon = themeToggle.querySelector("i");
        
        function updateIcon(theme) {
            if (theme === "light") {
                icon.className = "bi bi-moon-stars-fill";
            } else {
                icon.className = "bi bi-sun-fill";
            }
        }
        
        // Initialize icon state
        const currentTheme = document.documentElement.getAttribute("data-theme") || "dark";
        updateIcon(currentTheme);
        
        // Toggle theme on click
        themeToggle.addEventListener("click", function () {
            const activeTheme = document.documentElement.getAttribute("data-theme") === "light" ? "dark" : "light";
            document.documentElement.setAttribute("data-theme", activeTheme);
            localStorage.setItem("theme", activeTheme);
            updateIcon(activeTheme);
        });
    }

    // Parallax mouse effect for floating 3D/clay assets in hero section
    const hero = document.querySelector(".hero-section");
    const floatingAssets = document.querySelectorAll(".floating-asset");
    
    if (hero && floatingAssets.length > 0) {
        hero.addEventListener("mousemove", function (e) {
            const rect = hero.getBoundingClientRect();
            const x = e.clientX - rect.left - rect.width / 2;
            const y = e.clientY - rect.top - rect.height / 2;
            
            floatingAssets.forEach((asset, idx) => {
                // Different depth coefficients for layered depth
                const depth = (idx + 1) * 0.04;
                const moveX = x * depth;
                const moveY = y * depth;
                
                // Adjust margins to preserve keyframe CSS transform animations
                asset.style.marginLeft = `${moveX}px`;
                asset.style.marginTop = `${moveY}px`;
                asset.style.transition = "margin 0.1s ease-out";
            });
        });
        
        hero.addEventListener("mouseleave", function () {
            floatingAssets.forEach(asset => {
                asset.style.marginLeft = "0px";
                asset.style.marginTop = "0px";
                asset.style.transition = "margin 0.5s ease-out";
            });
        });
    }
});
